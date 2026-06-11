namespace Stone_Application
{
    partial class MainForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.panelNavigation = new System.Windows.Forms.Panel();
            this.panelHighlight = new System.Windows.Forms.Panel();
            this.buttonHome = new System.Windows.Forms.Button();
            this.buttonLog = new System.Windows.Forms.Button();
            this.buttonSettings = new System.Windows.Forms.Button();
            this.buttonExit = new System.Windows.Forms.Button();
            this.panelContent = new System.Windows.Forms.Panel();
            this.panelLoad = new System.Windows.Forms.Panel();
            this.panelTopBar = new System.Windows.Forms.Panel();
            this.labelTitle = new System.Windows.Forms.Label();
            this.buttonWindowToggle = new System.Windows.Forms.Button();
            this.buttonWindowMinimize = new System.Windows.Forms.Button();
            this.panelNavigation.SuspendLayout();
            this.panelContent.SuspendLayout();
            this.panelTopBar.SuspendLayout();
            this.SuspendLayout();
            // 
            // panelNavigation
            // 
            this.panelNavigation.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(75)))), ((int)(((byte)(22)))), ((int)(((byte)(76)))));
            this.panelNavigation.Controls.Add(this.panelHighlight);
            this.panelNavigation.Controls.Add(this.buttonHome);
            this.panelNavigation.Controls.Add(this.buttonLog);
            this.panelNavigation.Controls.Add(this.buttonSettings);
            this.panelNavigation.Controls.Add(this.buttonExit);
            this.panelNavigation.Dock = System.Windows.Forms.DockStyle.Left;
            this.panelNavigation.Location = new System.Drawing.Point(0, 0);
            this.panelNavigation.Name = "panelNavigation";
            this.panelNavigation.Size = new System.Drawing.Size(200, 800);
            this.panelNavigation.TabIndex = 0;
            // 
            // panelHighlight
            // 
            this.panelHighlight.BackColor = System.Drawing.Color.Violet;
            this.panelHighlight.Location = new System.Drawing.Point(0, 560);
            this.panelHighlight.Name = "panelHighlight";
            this.panelHighlight.Size = new System.Drawing.Size(3, 60);
            this.panelHighlight.TabIndex = 4;
            // 
            // buttonHome
            // 
            this.buttonHome.BackColor = System.Drawing.Color.DarkMagenta;
            this.buttonHome.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.buttonHome.FlatAppearance.BorderSize = 0;
            this.buttonHome.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.buttonHome.Font = new System.Drawing.Font("Nirmala UI", 10.2F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.buttonHome.ForeColor = System.Drawing.SystemColors.Control;
            this.buttonHome.Location = new System.Drawing.Point(0, 560);
            this.buttonHome.Name = "buttonHome";
            this.buttonHome.Size = new System.Drawing.Size(200, 60);
            this.buttonHome.TabIndex = 3;
            this.buttonHome.Text = "Home";
            this.buttonHome.UseVisualStyleBackColor = false;
            this.buttonHome.Click += new System.EventHandler(this.buttonHomeClick);
            // 
            // buttonLog
            // 
            this.buttonLog.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.buttonLog.FlatAppearance.BorderSize = 0;
            this.buttonLog.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.buttonLog.Font = new System.Drawing.Font("Nirmala UI", 10.2F, System.Drawing.FontStyle.Bold);
            this.buttonLog.ForeColor = System.Drawing.SystemColors.Control;
            this.buttonLog.Location = new System.Drawing.Point(0, 620);
            this.buttonLog.Name = "buttonLog";
            this.buttonLog.Size = new System.Drawing.Size(200, 60);
            this.buttonLog.TabIndex = 2;
            this.buttonLog.Text = "Logs";
            this.buttonLog.UseVisualStyleBackColor = true;
            this.buttonLog.Click += new System.EventHandler(this.buttonLogClick);
            // 
            // buttonSettings
            // 
            this.buttonSettings.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.buttonSettings.FlatAppearance.BorderSize = 0;
            this.buttonSettings.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.buttonSettings.Font = new System.Drawing.Font("Nirmala UI", 10.2F, System.Drawing.FontStyle.Bold);
            this.buttonSettings.ForeColor = System.Drawing.SystemColors.Control;
            this.buttonSettings.Location = new System.Drawing.Point(0, 680);
            this.buttonSettings.Name = "buttonSettings";
            this.buttonSettings.Size = new System.Drawing.Size(200, 60);
            this.buttonSettings.TabIndex = 1;
            this.buttonSettings.Text = "Settings";
            this.buttonSettings.UseVisualStyleBackColor = true;
            this.buttonSettings.Click += new System.EventHandler(this.buttonSettingsClick);
            // 
            // buttonExit
            // 
            this.buttonExit.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.buttonExit.FlatAppearance.BorderSize = 0;
            this.buttonExit.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.buttonExit.Font = new System.Drawing.Font("Nirmala UI", 10.2F, System.Drawing.FontStyle.Bold);
            this.buttonExit.ForeColor = System.Drawing.SystemColors.Control;
            this.buttonExit.Location = new System.Drawing.Point(0, 740);
            this.buttonExit.Name = "buttonExit";
            this.buttonExit.Size = new System.Drawing.Size(200, 60);
            this.buttonExit.TabIndex = 0;
            this.buttonExit.Text = "Exit";
            this.buttonExit.UseVisualStyleBackColor = true;
            this.buttonExit.Click += new System.EventHandler(this.buttonExitClick);
            // 
            // panelContent
            // 
            this.panelContent.Controls.Add(this.panelLoad);
            this.panelContent.Controls.Add(this.panelTopBar);
            this.panelContent.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelContent.Location = new System.Drawing.Point(200, 0);
            this.panelContent.Name = "panelContent";
            this.panelContent.Size = new System.Drawing.Size(1080, 800);
            this.panelContent.TabIndex = 1;
            // 
            // panelLoad
            // 
            this.panelLoad.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelLoad.Name = "panelLoad";
            this.panelLoad.TabIndex = 1;
            // 
            // panelTopBar
            // 
            this.panelTopBar.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(235)))), ((int)(((byte)(229)))), ((int)(((byte)(236)))));
            this.panelTopBar.Controls.Add(this.labelTitle);
            this.panelTopBar.Controls.Add(this.buttonWindowToggle);
            this.panelTopBar.Controls.Add(this.buttonWindowMinimize);
            this.panelTopBar.Dock = System.Windows.Forms.DockStyle.Top;
            this.panelTopBar.Location = new System.Drawing.Point(0, 0);
            this.panelTopBar.Name = "panelTopBar";
            this.panelTopBar.Size = new System.Drawing.Size(1080, 48);
            this.panelTopBar.TabIndex = 0;
            this.panelTopBar.DoubleClick += new System.EventHandler(this.panelTopBarDoubleClick);
            this.panelTopBar.MouseDown += new System.Windows.Forms.MouseEventHandler(this.panelTopBarMouseDown);
            this.panelTopBar.MouseMove += new System.Windows.Forms.MouseEventHandler(this.panelTopBarMouseMove);
            this.panelTopBar.MouseUp += new System.Windows.Forms.MouseEventHandler(this.panelTopBarMouseUp);
            // 
            // labelTitle
            // 
            this.labelTitle.AutoSize = true;
            this.labelTitle.Font = new System.Drawing.Font("Nirmala UI", 10.2F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.labelTitle.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(75)))), ((int)(((byte)(22)))), ((int)(((byte)(76)))));
            this.labelTitle.Location = new System.Drawing.Point(16, 13);
            this.labelTitle.Name = "labelTitle";
            this.labelTitle.Size = new System.Drawing.Size(141, 23);
            this.labelTitle.TabIndex = 2;
            this.labelTitle.Text = "Stone Monitor UI";
            this.labelTitle.DoubleClick += new System.EventHandler(this.panelTopBarDoubleClick);
            this.labelTitle.MouseDown += new System.Windows.Forms.MouseEventHandler(this.panelTopBarMouseDown);
            this.labelTitle.MouseMove += new System.Windows.Forms.MouseEventHandler(this.panelTopBarMouseMove);
            this.labelTitle.MouseUp += new System.Windows.Forms.MouseEventHandler(this.panelTopBarMouseUp);
            // 
            // buttonWindowToggle
            // 
            this.buttonWindowToggle.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonWindowToggle.FlatAppearance.BorderSize = 0;
            this.buttonWindowToggle.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.buttonWindowToggle.Font = new System.Drawing.Font("Nirmala UI", 9F, System.Drawing.FontStyle.Bold);
            this.buttonWindowToggle.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(75)))), ((int)(((byte)(22)))), ((int)(((byte)(76)))));
            this.buttonWindowToggle.Location = new System.Drawing.Point(882, 6);
            this.buttonWindowToggle.Name = "buttonWindowToggle";
            this.buttonWindowToggle.Size = new System.Drawing.Size(90, 36);
            this.buttonWindowToggle.TabIndex = 1;
            this.buttonWindowToggle.Text = "Expand";
            this.buttonWindowToggle.UseVisualStyleBackColor = true;
            this.buttonWindowToggle.Click += new System.EventHandler(this.buttonWindowToggleClick);
            // 
            // buttonWindowMinimize
            // 
            this.buttonWindowMinimize.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonWindowMinimize.FlatAppearance.BorderSize = 0;
            this.buttonWindowMinimize.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.buttonWindowMinimize.Font = new System.Drawing.Font("Nirmala UI", 9F, System.Drawing.FontStyle.Bold);
            this.buttonWindowMinimize.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(75)))), ((int)(((byte)(22)))), ((int)(((byte)(76)))));
            this.buttonWindowMinimize.Location = new System.Drawing.Point(978, 6);
            this.buttonWindowMinimize.Name = "buttonWindowMinimize";
            this.buttonWindowMinimize.Size = new System.Drawing.Size(90, 36);
            this.buttonWindowMinimize.TabIndex = 0;
            this.buttonWindowMinimize.Text = "Minimize";
            this.buttonWindowMinimize.UseVisualStyleBackColor = true;
            this.buttonWindowMinimize.Click += new System.EventHandler(this.buttonWindowMinimizeClick);
            // 
            // MainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1280, 800);
            this.Controls.Add(this.panelContent);
            this.Controls.Add(this.panelNavigation);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            this.MinimumSize = new System.Drawing.Size(1280, 800);
            this.Name = "MainForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Form1";
            this.panelNavigation.ResumeLayout(false);
            this.panelContent.ResumeLayout(false);
            this.panelTopBar.ResumeLayout(false);
            this.panelTopBar.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Panel panelNavigation;
        private System.Windows.Forms.Button buttonExit;
        private System.Windows.Forms.Button buttonHome;
        private System.Windows.Forms.Button buttonLog;
        private System.Windows.Forms.Button buttonSettings;
        private System.Windows.Forms.Panel panelLoad;
        private System.Windows.Forms.Panel panelHighlight;
        private System.Windows.Forms.Panel panelContent;
        private System.Windows.Forms.Panel panelTopBar;
        private System.Windows.Forms.Label labelTitle;
        private System.Windows.Forms.Button buttonWindowToggle;
        private System.Windows.Forms.Button buttonWindowMinimize;
    }
}

