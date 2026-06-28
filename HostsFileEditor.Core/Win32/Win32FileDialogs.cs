using System;
using System.Runtime.InteropServices;
using System.Text;

namespace HostsFileEditor.Win32;

public static partial class Win32FileDialogs
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct OPENFILENAME
    {
        public int lStructSize;
        public IntPtr hwndOwner;
        public IntPtr hInstance;
        public IntPtr lpstrFilter;
        public IntPtr lpstrCustomFilter;
        public int nMaxCustFilter;
        public int nFilterIndex;
        public IntPtr lpstrFile;
        public int nMaxFile;
        public IntPtr lpstrFileTitle;
        public int nMaxFileTitle;
        public IntPtr lpstrInitialDir;
        public IntPtr lpstrTitle;
        public int Flags;
        public short nFileOffset;
        public short nFileExtension;
        public IntPtr lpstrDefExt;
        public IntPtr lCustData;
        public IntPtr lpfnHook;
        public IntPtr lpTemplateName;
        public IntPtr pvReserved;
        public int dwReserved;
        public int FlagsEx;
    }

    private const int OFN_EXPLORER = 0x00080000;
    private const int OFN_FILEMUSTEXIST = 0x00001000;
    private const int OFN_PATHMUSTEXIST = 0x00000800;
    private const int OFN_OVERWRITEPROMPT = 0x00000002;

    [LibraryImport("comdlg32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetOpenFileName(ref OPENFILENAME ofn);

    [LibraryImport("comdlg32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetSaveFileName(ref OPENFILENAME ofn);

    public static string? OpenFileDialog(IntPtr ownerHwnd, string filter)
        => ShowDialog(ownerHwnd, filter, save: false, defaultExt: null, suggestedFileName: null);

    public static string? SaveFileDialog(IntPtr ownerHwnd, string suggestedFileName, string filter, string? defaultExt = null)
        => ShowDialog(ownerHwnd, filter, save: true, defaultExt: defaultExt, suggestedFileName: suggestedFileName);

    private static string? ShowDialog(IntPtr ownerHwnd, string filter, bool save, string? defaultExt, string? suggestedFileName)
    {
        var nativeFilter = BuildFilter(filter);
        var hFilter = IntPtr.Zero;
        var hTitle = IntPtr.Zero;
        var hDefExt = IntPtr.Zero;
        var hFileBuf = IntPtr.Zero;
        var hFileTitleBuf = IntPtr.Zero;
        try
        {
            hFilter = Marshal.StringToHGlobalUni(nativeFilter);
            var title = save ? "Save As" : "Open";
            hTitle = Marshal.StringToHGlobalUni(title);
            if (!string.IsNullOrWhiteSpace(defaultExt))
            {
                hDefExt = Marshal.StringToHGlobalUni(defaultExt!.TrimStart('.'));
            }

            const int FILE_BUFFER_CHARS = 4096;
            const int FILE_TITLE_CHARS = 256;
            var fileBytes = FILE_BUFFER_CHARS * 2; // Unicode char size
            var fileTitleBytes = FILE_TITLE_CHARS * 2;
            hFileBuf = Marshal.AllocHGlobal(fileBytes);
            hFileTitleBuf = Marshal.AllocHGlobal(fileTitleBytes);
            ClearBuffer(hFileBuf, fileBytes);
            ClearBuffer(hFileTitleBuf, fileTitleBytes);

            if (!string.IsNullOrWhiteSpace(suggestedFileName))
            {
                var chars = (suggestedFileName + '\0').ToCharArray();
                Marshal.Copy(chars, 0, hFileBuf, Math.Min(chars.Length, FILE_BUFFER_CHARS));
            }

            var ofn = new OPENFILENAME
            {
                lStructSize = Marshal.SizeOf<OPENFILENAME>(),
                hwndOwner = ownerHwnd,
                lpstrFilter = hFilter,
                nFilterIndex = 1,
                lpstrFile = hFileBuf,
                nMaxFile = FILE_BUFFER_CHARS,
                lpstrFileTitle = hFileTitleBuf,
                nMaxFileTitle = FILE_TITLE_CHARS,
                lpstrTitle = hTitle,
                Flags = OFN_EXPLORER | OFN_PATHMUSTEXIST | (save ? OFN_OVERWRITEPROMPT : OFN_FILEMUSTEXIST),
                lpstrDefExt = hDefExt
            };

            var ok = save ? GetSaveFileName(ref ofn) : GetOpenFileName(ref ofn);

            return !ok ? null : Marshal.PtrToStringUni(ofn.lpstrFile);
        }
        finally
        {
            FreeHGlobal(hFilter);
            FreeHGlobal(hTitle);
            FreeHGlobal(hDefExt);
            FreeHGlobal(hFileBuf);
            FreeHGlobal(hFileTitleBuf);
        }
    }

    private static void FreeHGlobal(IntPtr ptr)
    {
        if (ptr != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(ptr);
        }
    }

    private static string BuildFilter(string filter)
    {
        if (string.IsNullOrWhiteSpace(filter))
        {
            return "All Files (*.*)\0*.*\0\0";
        }
        var parts = filter.Split('|');
        var sb = new StringBuilder();
        for (var i = 0; i < parts.Length - 1; i += 2)
        {
            sb.Append(parts[i]);
            sb.Append('\0');
            sb.Append(parts[i + 1]);
            sb.Append('\0');
        }
        sb.Append('\0');
        return sb.ToString();
    }

    private static void ClearBuffer(IntPtr ptr, int bytes)
    {
        var zero = new byte[bytes];
        Marshal.Copy(zero, 0, ptr, bytes);
    }
}
