using System;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Windows.Forms;
using System.Windows.Forms.Integration;

namespace Veloxap.AddIn.Erwin
{
    /// <summary>
    /// Native WinForms shell for the erwin add-in. The existing WPF UI is
    /// hosted inside it so that erwin only needs to manage a WinForms window.
    /// </summary>
    internal sealed class ErwinAddInForm : Form
    {
        private ElementHost wpfHost;
        private bool contentInitializationQueued;

        internal Exception StartupError { get; private set; }

        internal ErwinAddInForm()
        {
            Text = "Veloxap Erwin Add-In";
            Icon = SystemIcons.Application;
            AutoScaleMode = AutoScaleMode.None;
            ClientSize = new Size(1100, 700);
            MinimumSize = new Size(950, 600);
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);

            if (contentInitializationQueued || IsDisposed || Disposing || !IsHandleCreated)
                return;

            contentInitializationQueued = true;
            // Create the native form and enter its modal message loop first.
            // WPF and asynchronous startup then run inside that form.
            BeginInvoke(new Action(() =>
            {
                try
                {
                    InitializeWpfContent();
                }
                catch (Exception ex)
                {
                    StartupError = ex;
                    Close();
                }
            }));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void InitializeWpfContent()
        {
            if (IsDisposed || Disposing)
                return;

            wpfHost = new ElementHost
            {
                Dock = DockStyle.Fill,
                AutoSize = false
            };

            var wpfContent = new Window1();
            wpfHost.Child = wpfContent;
            Controls.Add(wpfHost);

            var app = new SCAPI.Application();
            wpfContent.Init(ref app);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && wpfHost != null)
                wpfHost.Child = null;

            base.Dispose(disposing);
        }
    }
}
