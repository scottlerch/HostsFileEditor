namespace HostsFileEditor.Utilities;

public static class FileEx
{
    public static IDisposable DisableAttributes(string filePath, FileAttributes attributes) => new AttributeDisabler(filePath, attributes);

    private class AttributeDisabler : IDisposable
    {
        private readonly bool _areAttributesDisabled;
        private readonly FileAttributes _originalAttributes;
        private readonly FileAttributes _disableAttributes;
        private readonly string _filePath;

        public AttributeDisabler(string filePath, FileAttributes disableAttributes)
        {
            ArgumentNullException.ThrowIfNull(filePath);

            _filePath = filePath;
            _disableAttributes = disableAttributes;

            if (File.Exists(filePath))
            {
                _originalAttributes = File.GetAttributes(filePath);
                _areAttributesDisabled = _originalAttributes.HasFlag(disableAttributes);

                if (_areAttributesDisabled)
                {
                    File.SetAttributes(filePath, ~_disableAttributes & _originalAttributes);
                }
            }
        }

        public void Dispose()
        {
            if (_areAttributesDisabled && File.Exists(_filePath))
            {
                File.SetAttributes(_filePath, _originalAttributes);
            }
        }
    }
}
