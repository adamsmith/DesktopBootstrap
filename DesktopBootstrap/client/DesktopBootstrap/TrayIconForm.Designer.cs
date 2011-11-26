namespace DesktopBootstrap {
    partial class TrayIconForm {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing) {
            if (disposing && (components != null)) {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent() {
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(TrayIconForm));
            this.trayIcon = new System.Windows.Forms.NotifyIcon(this.components);
            this.trayMenu = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.toggleStartupWithWindowsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toggleAutomaticUpdatesToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
            this.quitDesktopBootstrapToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.hideTimer = new System.Windows.Forms.Timer(this.components);
            this.trayMenu.SuspendLayout();
            this.SuspendLayout();
            // 
            // trayIcon
            // 
            this.trayIcon.ContextMenuStrip = this.trayMenu;
            this.trayIcon.Icon = ((System.Drawing.Icon)(resources.GetObject("trayIcon.Icon")));
            this.trayIcon.Text = "DesktopBootstrap";
            this.trayIcon.Visible = true;
            this.trayIcon.MouseClick += new System.Windows.Forms.MouseEventHandler(this.trayIcon_MouseClick);
            // 
            // trayMenu
            // 
            this.trayMenu.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toggleStartupWithWindowsToolStripMenuItem,
            this.toggleAutomaticUpdatesToolStripMenuItem,
            this.toolStripSeparator1,
            this.quitDesktopBootstrapToolStripMenuItem});
            this.trayMenu.Name = "trayMenu";
            this.trayMenu.Size = new System.Drawing.Size(231, 76);
            // 
            // toggleStartupWithWindowsToolStripMenuItem
            // 
            this.toggleStartupWithWindowsToolStripMenuItem.Name = "toggleStartupWithWindowsToolStripMenuItem";
            this.toggleStartupWithWindowsToolStripMenuItem.Size = new System.Drawing.Size(230, 22);
            this.toggleStartupWithWindowsToolStripMenuItem.Text = "Disable startup with Windows";
            this.toggleStartupWithWindowsToolStripMenuItem.Click += new System.EventHandler(this.toggleStartWithWindowsToolStripMenuItem_Click);
            // 
            // toggleAutomaticUpdatesToolStripMenuItem
            // 
            this.toggleAutomaticUpdatesToolStripMenuItem.Name = "toggleAutomaticUpdatesToolStripMenuItem";
            this.toggleAutomaticUpdatesToolStripMenuItem.Size = new System.Drawing.Size(230, 22);
            this.toggleAutomaticUpdatesToolStripMenuItem.Text = "Disable automatic updates";
            this.toggleAutomaticUpdatesToolStripMenuItem.Click += new System.EventHandler(this.toggleAutomaticUpdatesToolStripMenuItem_Click);
            // 
            // toolStripSeparator1
            // 
            this.toolStripSeparator1.Name = "toolStripSeparator1";
            this.toolStripSeparator1.Size = new System.Drawing.Size(227, 6);
            // 
            // quitDesktopBootstrapToolStripMenuItem
            // 
            this.quitDesktopBootstrapToolStripMenuItem.Name = "quitDesktopBootstrapToolStripMenuItem";
            this.quitDesktopBootstrapToolStripMenuItem.Size = new System.Drawing.Size(230, 22);
            this.quitDesktopBootstrapToolStripMenuItem.Text = "Quit DesktopBootstrap";
            this.quitDesktopBootstrapToolStripMenuItem.Click += new System.EventHandler(this.quitDesktopBootstrapToolStripMenuItem_Click);
            // 
            // hideTimer
            // 
            this.hideTimer.Tick += new System.EventHandler(this.hideTimer_Tick);
            // 
            // TrayIconForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(31, 20);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "TrayIconForm";
            this.ShowInTaskbar = false;
            this.Text = "DesktopBootstrap";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.TrayIconForm_FormClosing);
            this.VisibleChanged += new System.EventHandler(this.TrayIconForm_VisibleChanged);
            this.trayMenu.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.NotifyIcon trayIcon;
        private System.Windows.Forms.ContextMenuStrip trayMenu;
        private System.Windows.Forms.ToolStripMenuItem quitDesktopBootstrapToolStripMenuItem;
        private System.Windows.Forms.Timer hideTimer;
        private System.Windows.Forms.ToolStripMenuItem toggleAutomaticUpdatesToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator1;
        private System.Windows.Forms.ToolStripMenuItem toggleStartupWithWindowsToolStripMenuItem;
    }
}

