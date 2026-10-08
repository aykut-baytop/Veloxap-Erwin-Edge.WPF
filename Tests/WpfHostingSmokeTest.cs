using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Controls;
using System.Windows;
using System.Windows.Input;
using System.Windows.Forms.Integration;
using Forms = System.Windows.Forms;

public static class WpfHostingSmokeTest
{
    private delegate IntPtr HookCallback(int code, IntPtr window, IntPtr data);

    public static int Run(string argument)
    {
        try
        {
            Require(Assembly.GetEntryAssembly() == null, "Test must run inside a native CLR host.");
            var parts = argument.Split('|');
            var assembly = Assembly.LoadFrom(parts[0]);
            Require(!IsWpfLoaded(), "WPF loaded before the bootstrap.");

            if (parts[1] != "baseline")
            {
                // Exercise the real COM entry point. Close the native shell at
                // activation, before its queued SCAPI/service initialization.
                int openedForms = 0;
                HookCallback callback = (code, window, data) =>
                {
                    if (code == 5) // HCBT_ACTIVATE
                    {
                        var title = new StringBuilder(256);
                        GetWindowText(window, title, title.Capacity);
                        if (title.ToString() == "Veloxap Erwin Add-In")
                        {
                            openedForms++;
                            var form = Forms.Control.FromHandle(window) as Forms.Form;
                            form.GetType().GetField("contentInitializationQueued", BindingFlags.Instance | BindingFlags.NonPublic)
                                .SetValue(form, true);
                            form.Close();
                        }
                    }
                    return CallNextHookEx(IntPtr.Zero, code, window, data);
                };
                IntPtr hook = SetWindowsHookEx(5, callback, IntPtr.Zero, GetCurrentThreadId());
                Require(hook != IntPtr.Zero, "Could not install the form activation hook.");
                try
                {
                    var managerType = assembly.GetType("Veloxap.AddIn.COMVeloxapManagerClass", true);
                    managerType.GetMethod("Run").Invoke(Activator.CreateInstance(managerType), null);
                    Require(openedForms == 1, "COM Run did not start exactly one ErwinAddInForm.");
                }
                finally
                {
                    UnhookWindowsHookEx(hook);
                    GC.KeepAlive(callback);
                }

                // Repeated startup must keep an already fixed DPI level intact.
                assembly.GetType("Veloxap.AddIn.Erwin.HostDpiAwareness", true)
                    .GetMethod("Preserve", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
            }

            LoadEmbeddedWpf(assembly);
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            return 1;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void LoadEmbeddedWpf(Assembly assembly)
    {
        var formType = assembly.GetType("Veloxap.AddIn.Erwin.ErwinAddInForm", true);
        using (var form = (Forms.Form)Activator.CreateInstance(formType, true))
        {
            Require(form.Controls.Count == 0, "WPF content was created in the form constructor.");
            var view = Activator.CreateInstance(assembly.GetType("Veloxap.AddIn.Erwin.Window1", true)) as UserControl;
            Require(view != null, "The main WPF screen must be a UserControl.");
            using (var elementHost = new ElementHost { Dock = Forms.DockStyle.Fill, Child = view })
            {
                form.Controls.Add(elementHost);
                var handle = form.Handle;
                var childHandle = elementHost.Handle;
                Require(elementHost.FindForm() == form, "WPF is not embedded in ErwinAddInForm.");
                Require(elementHost.Size == form.ClientSize, "WPF content does not fill the native form.");
                form.ClientSize = new System.Drawing.Size(1200, 800);
                Require(elementHost.Size == form.ClientSize, "WPF content does not follow form resizing.");

                var validationType = assembly.GetType("Veloxap.AddIn.Erwin.Pages.ModelValidationView", true);
                var validationView = (UserControl)Activator.CreateInstance(validationType);
                elementHost.Child = validationView;
                Forms.Application.DoEvents();
                CheckApprovalDialog(validationView, form, "cancel");
                CheckApprovalDialog(validationView, form, "escape");
                CheckApprovalDialog(validationView, form, "confirm");
                CheckApprovalDialog(validationView, form, "enter");
                elementHost.Child = null;
            }
        }
    }

    private static void CheckApprovalDialog(UserControl view, Forms.Form owner, string action)
    {
        Exception failure = null;
        var started = DateTime.UtcNow;
        bool handled = false;
        using (var timer = new Forms.Timer { Interval = 25 })
        {
            timer.Tick += (sender, args) =>
            {
                var dialog = Forms.Application.OpenForms.Cast<Forms.Form>().FirstOrDefault(f => f.Text == "Onay talebi");
                try
                {
                    Require(DateTime.UtcNow - started < TimeSpan.FromSeconds(10), "Approval dialog timed out.");
                    if (dialog == null)
                        return;
                    timer.Stop();
                    handled = true;
                    Require(dialog.Owner == owner, "Approval dialog must be owned by ErwinAddInForm.");
                    var host = dialog.Controls.OfType<ElementHost>().Single();
                    var root = (Grid)host.Child;
                    var boxes = root.Children.OfType<TextBox>().ToArray();
                    var buttons = root.Children.OfType<StackPanel>().Single().Children.OfType<Button>().ToArray();
                    Require(!buttons[1].IsEnabled, "Empty approval input must not be accepted.");
                    if (action == "cancel")
                        buttons[0].RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    else if (action == "escape")
                        RaiseKey(root, Key.Escape);
                    else
                    {
                        boxes[0].Text = "  VELOX-42  ";
                        boxes[1].Text = "short";
                        buttons[1].RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                        Require(dialog.Visible && dialog.DialogResult != Forms.DialogResult.OK,
                            "An insufficient approval description must keep the dialog open.");
                        boxes[1].Text = "  Valid description  ";
                        if (action == "enter")
                        {
                            Keyboard.Focus(boxes[0]);
                            RaiseKey(root, Key.Enter);
                        }
                        else
                            buttons[1].RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    }
                }
                catch (Exception ex)
                {
                    failure = ex;
                    timer.Stop();
                    if (dialog != null)
                        dialog.Close();
                }
            };
            timer.Start();
            object[] arguments = { null };
            var description = view.GetType().GetMethod("PromptForValidationDescription", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(view, arguments) as string;
            if (failure != null)
                throw failure;
            Require(handled, "Approval dialog was not opened.");
            bool confirmed = action == "confirm" || action == "enter";
            Require(description == (confirmed ? "Valid description" : null), "Incorrect approval description result.");
            Require((string)arguments[0] == (confirmed ? "VELOX-42" : null), "Incorrect Jira number result.");
        }
    }

    private static void RaiseKey(Grid root, Key key)
    {
        root.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(root), 0, key)
        {
            RoutedEvent = Keyboard.PreviewKeyDownEvent
        });
    }

    private static bool IsWpfLoaded()
    {
        return AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == "PresentationCore");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    [DllImport("user32.dll")]
    private static extern IntPtr SetWindowsHookEx(int hookId, HookCallback callback, IntPtr module, uint threadId);
    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr window, IntPtr data);
    [DllImport("user32.dll")]
    private static extern bool UnhookWindowsHookEx(IntPtr hook);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr window, StringBuilder text, int maxCount);
    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();
}
