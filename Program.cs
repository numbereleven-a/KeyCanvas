namespace KeyCanvas;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        // A second canvas would compete for input and hide the first one's exit gesture.
        using var instance = new Mutex(true, "KeyCanvas.Desktop", out bool firstInstance);
        if (!firstInstance)
        {
            nint canvas = NativeMethods.FindWindow(null, "KeyCanvas");
            if (canvas != 0)
                NativeMethods.SetForegroundWindow(NativeMethods.GetLastActivePopup(canvas));
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
        try
        {
            using var canvas = new CanvasForm();
            Application.Run(canvas);
        }
        catch (Exception)
        {
            // Dispose the canvas and its hook before showing an ordinary Windows dialog.
            MessageBox.Show(UiText.Pick(SettingsStore.Load(SettingsStore.FilePath).Language,
                "KeyCanvas could not continue. Control has been returned to Windows.",
                "KeyCanvas не удалось продолжить работу. Управление возвращено Windows."),
                "KeyCanvas", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
