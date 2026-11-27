using NT.Diagnostics;
using NT.Tools;
using NT.Localization;
using System.ComponentModel;
using System.Drawing.Design;
using System.Globalization;
using System.Security;
using System.Text;

namespace RouterXYZACRTCP;

/// <summary>Imports the customer-delivered activation package without exposing direct text editing.</summary>
internal sealed class LicenseFileEditor : UITypeEditor
{
    private const int MaximumCharacters = 4096;

    public override UITypeEditorEditStyle GetEditStyle(ITypeDescriptorContext? context)
        => context?.PropertyDescriptor?.IsReadOnly == true
            ? UITypeEditorEditStyle.None
            : UITypeEditorEditStyle.Modal;

    public override object? EditValue(
        ITypeDescriptorContext? context,
        IServiceProvider provider,
        object? value)
    {
        if (context?.PropertyDescriptor?.IsReadOnly == true)
            return value;

        using var dialog = new OpenFileDialog
        {
            Title = NTLocalization.Get(RouterXYZACRTCPTexts.ImportLicenseTitle),
            Filter = NTLocalization.Get(RouterXYZACRTCPTexts.LicenseFilter),
            DefaultExt = "ntlic",
            AddExtension = true,
            CheckFileExists = true,
            CheckPathExists = true,
            Multiselect = false,
            RestoreDirectory = true,
        };

        if (dialog.ShowDialog() != DialogResult.OK)
            return value;

        try
        {
            string content = ReadBoundedText(dialog.FileName);
            if (!IsImportablePackage(content))
            {
                throw new InvalidDataException(
                    "The selected file does not contain a valid Radonix RNT3 activation package.");
            }

            // Preserve the file text in the Link. LinkManager persists this exact string;
            // Controller performs its existing transport normalization at connection time.
            return content;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or
                                      SecurityException or DecoderFallbackException or InvalidDataException)
        {
            sMessage.Show(null, new SMessageRequest(NTLocalization.Get(RouterXYZACRTCPTexts.ActivationImportFailed))
            {
                Title = NTLocalization.Get(RouterXYZACRTCPTexts.LicenseTitle),
                Severity = SMessageSeverity.Warning,
                TechnicalDetails = error.Message,
                Exception = error
            });
            return value;
        }
    }

    internal static bool IsImportablePackage(string? content)
    {
        string normalized = AcontisLicenseDiagnostic.NormalizeKeyForTransport(content ?? string.Empty);
        return normalized != "INVALID" && normalized.StartsWith("RNT3.", StringComparison.Ordinal);
    }

    internal static string ReadBoundedText(string fileName)
    {
        using var stream = new FileStream(
            fileName,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 4096,
            FileOptions.SequentialScan);
        using var reader = new StreamReader(
            stream,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true),
            detectEncodingFromByteOrderMarks: true);

        var buffer = new char[MaximumCharacters + 1];
        int length = 0;
        while (length < buffer.Length)
        {
            int read = reader.Read(buffer, length, buffer.Length - length);
            if (read == 0)
                break;
            length += read;
        }

        if (length > MaximumCharacters || reader.Read() != -1)
            throw new InvalidDataException("The selected license file is larger than 4096 characters.");

        return new string(buffer, 0, length);
    }
}

/// <summary>Displays only license presence and rejects direct PropertyGrid text conversion.</summary>
internal sealed class LicenseFileContentConverter : TypeConverter
{
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
        => sourceType != typeof(string) && base.CanConvertFrom(context, sourceType);

    public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType)
        => destinationType == typeof(string) || base.CanConvertTo(context, destinationType);

    public override object ConvertFrom(
        ITypeDescriptorContext? context,
        CultureInfo? culture,
        object value)
    {
        if (value is string)
            throw new NotSupportedException("Use the dialog button to import a .ntlic file.");

        return base.ConvertFrom(context, culture, value)!;
    }

    public override object? ConvertTo(
        ITypeDescriptorContext? context,
        CultureInfo? culture,
        object? value,
        Type destinationType)
    {
        if (destinationType == typeof(string))
            return LicenseFileEditor.IsImportablePackage(value?.ToString())
                ? "License file imported"
                : "No license file imported";

        return base.ConvertTo(context, culture, value, destinationType);
    }
}
