using System;
using System.Drawing;
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
        private readonly ElementHost wpfHost;

        internal ErwinAddInForm(SCAPI.Application app)
        {
            if (app == null)
                throw new ArgumentNullException(nameof(app));

            Text = "Veloxap Erwin Add-In";
            ClientSize = new Size(1100, 700);
            MinimumSize = new Size(950, 600);
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;

            wpfHost = new ElementHost
            {
                Dock = DockStyle.Fill
            };

            var wpfContent = new Window1();
            wpfContent.Init(ref app);
            wpfHost.Child = wpfContent;
            Controls.Add(wpfHost);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && wpfHost != null)
                wpfHost.Child = null;

            base.Dispose(disposing);
        }
    }
}
