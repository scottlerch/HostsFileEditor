namespace HostsFileEditor.Utilities;

public static class FileEx
{
    public static IDisposable DisableAttributes(string filePath, FileAttributes attributes)
    {
        return new AttributeDisabler(filePath, attributes);
    }

    private class AttributeDisabler : IDisposable
    {
        private readonly bool areAttributesDisabled;
        private readonly FileAttributes originalAttributes;
        private readonly FileAttributes disableAttributes;
        private readonly string filePath;

        public AttributeDisabler(string filePath, FileAttributes disableAttributes)
        {
            ArgumentNullException.ThrowIfNull(filePath);

            this.filePath = filePath;
            this.disableAttributes = disableAttributes;

            if (File.Exists(filePath))
            {
                originalAttributes = File.GetAttributes(filePath);
                areAttributesDisabled = originalAttributes.HasFlag(disableAttributes);

                if (areAttributesDisabled)
                {
                    File.SetAttributes(filePath, ~this.disableAttributes & originalAttributes);
                }
            }
        }

        public void Dispose()
        {
            if (areAttributesDisabled && File.Exists(filePath))
            {
                File.SetAttributes(filePath, originalAttributes);
            }
        }
    }
}
