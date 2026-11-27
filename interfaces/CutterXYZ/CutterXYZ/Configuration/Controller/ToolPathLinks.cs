using NT;
using NT.Controller;
using NT.Diagnostics;
using NT.LinkManager;

namespace CutterXYZ
{
    /// <summary>
    /// ToolPath-specific portion of <see cref="ControllerLinks"/>. It configures
    /// all ToolPaths. Run targets TP0; Reset stops all motion and then rewinds TP0.
    /// </summary>
    internal sealed partial class ControllerLinks
    {
        private readonly ToolPathWrapper mainToolPath;

        private readonly LinkGroup toolPathsGroup;
        private readonly LinkGroup[] toolPathGroups;
        private bool resetMainPathAfterStop;

        internal CommandLink RunLink { get; private set; } = null!;
        internal CommandLink ResetLink { get; private set; } = null!;
        private HeadPositioningCommands? headPositioning;
        internal CommandLink ReturnToWorkLink => headPositioning!.ReturnToWorkLink;
        internal bool ReturnToWork() => headPositioning?.ReturnToWork() == true;
        internal bool RunOrPositionMainToolPath()
        {
            if (IsPauseSwitchOpen || headPositioning?.IsActive == true) return false;
            // TP0's mismatch handler intentionally rejects direct Run after
            // admitting TP1 positioning. That accepted handoff is a successful
            // operator request, even though TP0 has not started yet.
            return controller.RunToolPath(mainToolPath) || headPositioning?.IsReturningToWork == true;
        }
        internal CommandLink GoToReferenceLink => headPositioning!.GoToReferenceLink;
        internal CommandLink GoHomeLink => headPositioning!.GoHomeLink;
        internal CommandLink GoToParkLink => headPositioning!.GoToParkLink;
        internal bool GoToPark() => headPositioning?.GoToPark() == true;
        internal bool AreRunCommandsAvailable => CanRunMainToolPath();
        internal bool IsPauseSwitchOpen => OperatorSwitches.IsPauseRequested(controller.PLC.IOMappings["PauseSwitch"]);

        private static LinkGroup[] CreateToolPathGroups(LinkGroup parentGroup)
        {
            var groups = new LinkGroup[Initialize.ToolPathAxes.Length];

            for (int index = 0; index < groups.Length; index++)
            {
                LinkGroup toolPath = groups[index] = parentGroup.AddGroup(
                    $"ToolPath{index}", $"Tool Path {index}",
                    $"Configuration for coordinated motion ToolPath {index}.");

                toolPath.IconKey = "toolpath";
                toolPath.AddReal("AccelerationCoefficient", "Acceleration Coefficient", 1.0, 0.001, 1.0, 3, "ToolPath acceleration and deceleration multiplier.");
                toolPath.AddReal("StopVelocity", "Stop Velocity", 0.0, 0.0, 1000.0, 6, "Junction velocities below this cutoff in path engineering units per second become complete stops; the cutoff never raises a planned velocity.");
                toolPath.AddReal("MinimumLength", "Minimum Length", 0.0, 0.0, 1000.0, 6, "Minimum accepted AddLine distance in path engineering units; zero selects ten times the resultant ToolPath-axis step length.");
                toolPath.AddReal("ContourTolerance", "Contour Tolerance", 0.01, 0.0, 1000.0, 6, "Maximum contour deviation in path engineering units used for smooth automatic junction planning.");
            }

            return groups;
        }

        private void ApplyToolPathProperties()
        {
            for (int index = 0; index < toolPathGroups.Length; index++)
            {
                LinkGroup group = toolPathGroups[index];
                ToolPathWrapper toolPath = controller.ToolPaths[index];

                toolPath.AccelerationCoefficient = group.ReadReal("AccelerationCoefficient");
                toolPath.StopVelocity = group.ReadReal("StopVelocity");
                toolPath.MinimumLength = group.ReadReal("MinimumLength");
                toolPath.ContourTolerance = group.ReadReal("ContourTolerance");
                toolPath.SetToolPathAxes(Initialize.ToolPathAxes[index]);
            }
        }

        private void RefreshToolPathStates()
        {
            for (int index = 0; index < toolPathGroups.Length; index++)
            {
                ToolPathWrapper toolPath = controller.ToolPaths[index];
                LinkGroup group = toolPathGroups[index];
                if (!controller.IsConnected)
                    group.SetState(OperationalState.Inactive, "Controller is offline.");
                else if (toolPath.IsRunning || toolPath.IsStopping)
                    group.SetState(OperationalState.Active, "ToolPath is active.");
                else if (toolPath.IsReadyToMove || toolPath.IsReadyToRun)
                    group.SetState(OperationalState.Valid, "ToolPath is ready.");
                else
                    group.SetState(OperationalState.Inactive, "ToolPath is idle.");
            }

            SetAggregateState(toolPathsGroup, toolPathGroups);
        }

        private void CreateToolPathCommandLinks()
        {
            RunLink = CommandLink.CreateCommand("Run", DisplayNames.FromIdentifier("Run"));
            RunLink.ActionExecute += (_, _) =>
            {
                if (CanRunMainToolPath()) mainForm.RunMainProgram();
            };
            RunLink.QueryEnabled += (_, e) => e.Enabled = CanRunMainToolPath();

            ResetLink = CommandLink.CreateCommand("Reset", "Reset");
            ResetLink.ActionExecute += (_, _) => StopMotion();
            // Reset first stops every motion source, then rewinds TP0 when idle.
            // It remains available during pending starts, jog, homing and faults.
            ResetLink.QueryEnabled += (_, e) => e.Enabled = controller.IsConnected;
        }

        private bool CanRunMainToolPath()
        {
            if (IsPauseSwitchOpen || mainForm.IsProgramLoading) return false;
            // A stopped explicit restart can select existing TP0 rows even when its previous cursor has no rows left.
            bool canPrepareRequestedStart = mainForm.HasPendingProgramStart &&
                mainToolPath.IsEnded && mainToolPath.IsIdle && mainToolPath.IsStopped && mainToolPath.IsReadyToMove;
            if (headPositioning?.IsActive == true
                || resetMainPathAfterStop
                || mainToolPath.IsPausedForProgram
                || !canPrepareRequestedStart && !mainToolPath.IsReadyToStartOrResume
                || !CanStartHomedMotion())
            {
                return false;
            }

            return true;
        }

        internal bool CanStartHomedMotion()
        {
            if (IsPauseSwitchOpen || !mainForm.IsMachineOnline || mainForm.IsProgramLoading || IsHomingActive())
                return false;

            return mainToolPath.IsHomed;
        }

        internal void CreateHeadPositioningCommands(GeneralLinks generalLinks)
        {
            headPositioning = new HeadPositioningCommands(
                controller,
                () => generalLinks.TraverseVelocity,
                () => generalLinks.Positioning,
                () => CanStartHomedMotion(),
                generalLinks.GetParkPosition);
        }

        internal void CancelHeadPositioningContinuation() => headPositioning?.CancelContinuation();
        internal void DisposeHeadPositioningCommands() => headPositioning?.Dispose();

        internal bool UpdateStopReset()
        {
            // Reset TP0 only after the controller reports it idle. A loading worker
            // may own TP0 temporarily, so leave the pending reset for a later UI tick.
            if (mainForm.IsProgramLoading) return false;
            if (!resetMainPathAfterStop)
                return false;
            if (!controller.IsConnected)
            {
                resetMainPathAfterStop = false;
                return true;
            }
            if (!mainToolPath.IsIdle)
                return false;

            if (!controller.ResetToolPath(mainToolPath))
                return false;

            resetMainPathAfterStop = false;
            mainForm.OnMainProgramReset();
            return true;
        }

        /// <summary>Stops every motion source while preserving the main program cursor for continuation.</summary>
        internal void PauseMotion()
        {
            if (!controller.IsConnected) return;
            mainForm.ReportOperation("Pause requested: stop all motion and preserve the main program cursor");
            StopMotion(resetMainPath: false);
        }

        private void StopMotion() => StopMotion(resetMainPath: true);

        private void StopMotion(bool resetMainPath)
        {
            // Only explicit Reset schedules a fresh-start reset. Pause preserves
            // the native partial cursor; it must not cancel an earlier Reset request.
            CancelHeadPositioningContinuation();
            if (!controller.IsConnected)
                return;

            bool previousReset = resetMainPathAfterStop;
            if (resetMainPath) mainForm.ReportOperation("Reset requested: all motion; reset main program after stopping");
            resetMainPathAfterStop |= resetMainPath;
            if (!controller.Stop(StopFlags.AllMotion))
            {
                resetMainPathAfterStop = previousReset;
                NTLog.Error(
                    NTErrorCode.MotionCommandRejected,
                    NTErrorSeverity.Warning,
                    "Motion stop request was not accepted by the controller.");
                return;
            }

            mainForm.ReportOperation(resetMainPath ? "Reset accepted; waiting for motion to stop" : "Pause accepted; waiting for motion to stop");
            RunOperatorPlc(resetMainPath ? "Reset" : "Pause");
            // If TP0 is already idle (for example while only Jog is active),
            // reset it immediately. Otherwise the UI monitor retries after RT
            // completes the controlled stop and releases the path command.
            UpdateStopReset();
        }

        // Runs after command admission, not after physical standstill. The
        // engineer extension must not delay motion stopping or reset bookkeeping.
        internal void RunOperatorPlc(string name)
        {
            if (!TryGetPlcProgram(name, out var program) || !program.Run())
                NTLog.Error(NTErrorCode.PlcRunRequestRejected, NTErrorSeverity.RecoverableError,
                    $"Operator {name} PLC extension could not be started.");
        }
    }
}
