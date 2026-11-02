internal static class Program
{
    [STAThread]
    private static int Main()
    {
        try
        {
            Console.OutputEncoding=new System.Text.UTF8Encoding(false);
            Console.Error.Write("");
            new SunEyeVision.UI.Tests.RootWorkspaceHostRuntimeTests().Real_shell_root_copy_history_and_reopen_use_same_admitted_image_source();
            Console.WriteLine("root host probe passed");
            return 0;
        }
        catch(Exception error){Console.Error.WriteLine(error);return 1;}
    }
}
