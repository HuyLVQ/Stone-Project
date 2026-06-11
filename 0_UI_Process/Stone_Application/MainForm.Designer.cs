namespace Stone_Application
{
    partial class MainForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer m_components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool p_disposing)
        {
            if (p_disposing && (m_components != null))
            {
                m_components.Dispose();
            }
            base.Dispose(p_disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.m_panelNavigation = new System.Windows.Forms.Panel();
            this.m_panelHighlight = new System.Windows.Forms.Panel();
            this.m_buttonHome = new System.Windows.Forms.Button();
            this.m_buttonLog = new System.Windows.Forms.Button();
            this.m_buttonSettings = new System.Windows.Forms.Button();
            this.m_buttonExit = new System.Windows.Forms.Button();
            this.m_panelContent = new System.Windows.Forms.Panel();
            this.m_panelLoad = new System.Windows.Forms.Panel();
            this.m_panelTopBar = new System.Windows.Forms.Panel();
            this.m_labelTitle = new System.Windows.Forms.Label();
            this.m_buttonWindowToggle = new System.Windows.Forms.Button();
            this.m_buttonWindowMinimize = new System.Windows.Forms.Button();
            this.m_panelNavigation.SuspendLayout();
            this.m_panelContent.SuspendLayout();
            this.m_panelTopBar.SuspendLayout();
            this.SuspendLayout();
            // 
            // panelNavigation
            // 
            this.m_panelNavigation.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(75)))), ((int)(((byte)(22)))), ((int)(((byte)(76)))));
            this.m_panelNavigation.Controls.Add(this.m_panelHighlight);
            this.m_panelNavigation.Controls.Add(this.m_buttonHome);
            this.m_panelNavigation.Controls.Add(this.m_buttonLog);
            this.m_panelNavigation.Controls.Add(this.m_buttonSettings);
            this.m_panelNavigation.Controls.Add(this.m_buttonExit);
            this.m_panelNavigation.Dock = System.Windows.Forms.DockStyle.Left;
            this.m_panelNavigation.Location = new System.Drawing.Point(0, 0);
            this.m_panelNavigation.Name = "panelNavigation";
            this.m_panelNavigation.Size = new System.Drawing.Size(200, 800);
            this.m_panelNavigation.TabIndex = 0;
            // 
            // panelHighlight
            // 
            this.m_panelHighlight.BackColor = System.Drawing.Color.Violet;
            this.m_panelHighlight.Location = new System.Drawing.Point(0, 560);
            this.m_panelHighlight.Name = "panelHighlight";
            this.m_panelHighlight.Size = new System.Drawing.Size(3, 60);
            this.m_panelHighlight.TabIndex = 4;
            // 
            // buttonHome
            // 
            this.m_buttonHome.BackColor = System.Drawing.Color.DarkMagenta;
            this.m_buttonHome.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.m_buttonHome.FlatAppearance.BorderSize = 0;
            this.m_buttonHome.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.m_buttonHome.Font = new System.Drawing.Font("Nirmala UI", 10.2F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.m_buttonHome.ForeColor = System.Drawing.SystemColors.Control;
            this.m_buttonHome.Location = new System.Drawing.Point(0, 560);
            this.m_buttonHome.Name = "buttonHome";
            this.m_buttonHome.Size = new System.Drawing.Size(200, 60);
            this.m_buttonHome.TabIndex = 3;
            this.m_buttonHome.Text = "Home";
            this.m_buttonHome.UseVisualStyleBackColor = false;
            this.m_buttonHome.Click += new System.EventHandler(this.buttonHomeClick);
            // 
            // buttonLog
            // 
            this.m_buttonLog.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.m_buttonLog.FlatAppearance.BorderSize = 0;
            this.m_buttonLog.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.m_buttonLog.Font = new System.Drawing.Font("Nirmala UI", 10.2F, System.Drawing.FontStyle.Bold);
            this.m_buttonLog.ForeColor = System.Drawing.SystemColors.Control;
            this.m_buttonLog.Location = new System.Drawing.Point(0, 620);
            this.m_buttonLog.Name = "buttonLog";
            this.m_buttonLog.Size = new System.Drawing.Size(200, 60);
            this.m_buttonLog.TabIndex = 2;
            this.m_buttonLog.Text = "Logs";
            this.m_buttonLog.UseVisualStyleBackColor = true;
            this.m_buttonLog.Click += new System.EventHandler(this.buttonLogClick);
            // 
            // buttonSettings
            // 
            this.m_buttonSettings.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.m_buttonSettings.FlatAppearance.BorderSize = 0;
            this.m_buttonSettings.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.m_buttonSettings.Font = new System.Drawing.Font("Nirmala UI", 10.2F, System.Drawing.FontStyle.Bold);
            this.m_buttonSettings.ForeColor = System.Drawing.SystemColors.Control;
            this.m_buttonSettings.Location = new System.Drawing.Point(0, 680);
            this.m_buttonSettings.Name = "buttonSettings";
            this.m_buttonSettings.Size = new System.Drawing.Size(200, 60);
            this.m_buttonSettings.TabIndex = 1;
            this.m_buttonSettings.Text = "Settings";
            this.m_buttonSettings.UseVisualStyleBackColor = true;
            this.m_buttonSettings.Click += new System.EventHandler(this.buttonSettingsClick);
            // 
            // buttonExit
            // 
            this.m_buttonExit.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.m_buttonExit.FlatAppearance.BorderSize = 0;
            this.m_buttonExit.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.m_buttonExit.Font = new System.Drawing.Font("Nirmala UI", 10.2F, System.Drawing.FontStyle.Bold);
            this.m_buttonExit.ForeColor = System.Drawing.SystemColors.Control;
            this.m_buttonExit.Location = new System.Drawing.Point(0, 740);
            this.m_buttonExit.Name = "buttonExit";
            this.m_buttonExit.Size = new System.Drawing.Size(200, 60);
            this.m_buttonExit.TabIndex = 0;
            this.m_buttonExit.Text = "Exit";
            this.m_buttonExit.UseVisualStyleBackColor = true;
            this.m_buttonExit.Click += new System.EventHandler(this.buttonExitClick);
            // 
            // panelContent
            // 
            this.m_panelContent.Controls.Add(this.m_panelLoad);
            this.m_panelContent.Controls.Add(this.m_panelTopBar);
            this.m_panelContent.Dock = System.Windows.Forms.DockStyle.Fill;
            this.m_panelContent.Location = new System.Drawing.Point(200, 0);
            this.m_panelContent.Name = "panelContent";
            this.m_panelContent.Size = new System.Drawing.Size(1080, 800);
            this.m_panelContent.TabIndex = 1;
            // 
            // panelLoad
            // 
            this.m_panelLoad.Dock = System.Windows.Forms.DockStyle.Fill;
            this.m_panelLoad.Name = "panelLoad";
            this.m_panelLoad.TabIndex = 1;
            // 
            // panelTopBar
            // 
            this.m_panelTopBar.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(235)))), ((int)(((byte)(229)))), ((int)(((byte)(236)))));
            this.m_panelTopBar.Controls.Add(this.m_labelTitle);
            this.m_panelTopBar.Controls.Add(this.m_buttonWindowToggle);
            this.m_panelTopBar.Controls.Add(this.m_buttonWindowMinimize);
            this.m_panelTopBar.Dock = System.Windows.Forms.DockStyle.Top;
            this.m_panelTopBar.Location = new System.Drawing.Point(0, 0);
            this.m_panelTopBar.Name = "panelTopBar";
            this.m_panelTopBar.Size = new System.Drawing.Size(1080, 48);
            this.m_panelTopBar.TabIndex = 0;
            this.m_panelTopBar.DoubleClick += new System.EventHandler(this.panelTopBarDoubleClick);
            this.m_panelTopBar.MouseDown += new System.Windows.Forms.MouseEventHandler(this.panelTopBarMouseDown);
            this.m_panelTopBar.MouseMove += new System.Windows.Forms.MouseEventHandler(this.panelTopBarMouseMove);
            this.m_panelTopBar.MouseUp += new System.Windows.Forms.MouseEventHandler(this.panelTopBarMouseUp);
            // 
            // labelTitle
            // 
            this.m_labelTitle.AutoSize = true;
            this.m_labelTitle.Font = new System.Drawing.Font("Nirmala UI", 10.2F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.m_labelTitle.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(75)))), ((int)(((byte)(22)))), ((int)(((byte)(76)))));
            this.m_labelTitle.Location = new System.Drawing.Point(16, 13);
            this.m_labelTitle.Name = "labelTitle";
            this.m_labelTitle.Size = new System.Drawing.Size(141, 23);
            this.m_labelTitle.TabIndex = 2;
            this.m_labelTitle.Text = "Stone Monitor UI";
            this.m_labelTitle.DoubleClick += new System.EventHandler(this.panelTopBarDoubleClick);
            this.m_labelTitle.MouseDown += new System.Windows.Forms.MouseEventHandler(this.panelTopBarMouseDown);
            this.m_labelTitle.MouseMove += new System.Windows.Forms.MouseEventHandler(this.panelTopBarMouseMove);
            this.m_labelTitle.MouseUp += new System.Windows.Forms.MouseEventHandler(this.panelTopBarMouseUp);
            // 
            // buttonWindowToggle
            // 
            this.m_buttonWindowToggle.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.m_buttonWindowToggle.FlatAppearance.BorderSize = 0;
            this.m_buttonWindowToggle.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.m_buttonWindowToggle.Font = new System.Drawing.Font("Nirmala UI", 9F, System.Drawing.FontStyle.Bold);
            this.m_buttonWindowToggle.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(75)))), ((int)(((byte)(22)))), ((int)(((byte)(76)))));
            this.m_buttonWindowToggle.Location = new System.Drawing.Point(882, 6);
            this.m_buttonWindowToggle.Name = "buttonWindowToggle";
            this.m_buttonWindowToggle.Size = new System.Drawing.Size(90, 36);
            this.m_buttonWindowToggle.TabIndex = 1;
            this.m_buttonWindowToggle.Text = "Expand";
            this.m_buttonWindowToggle.UseVisualStyleBackColor = true;
            this.m_buttonWindowToggle.Click += new System.EventHandler(this.buttonWindowToggleClick);
            // 
            // buttonWindowMinimize
            // 
            this.m_buttonWindowMinimize.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.m_buttonWindowMinimize.FlatAppearance.BorderSize = 0;
            this.m_buttonWindowMinimize.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.m_buttonWindowMinimize.Font = new System.Drawing.Font("Nirmala UI", 9F, System.Drawing.FontStyle.Bold);
            this.m_buttonWindowMinimize.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(75)))), ((int)(((byte)(22)))), ((int)(((byte)(76)))));
            this.m_buttonWindowMinimize.Location = new System.Drawing.Point(978, 6);
            this.m_buttonWindowMinimize.Name = "buttonWindowMinimize";
            this.m_buttonWindowMinimize.Size = new System.Drawing.Size(90, 36);
            this.m_buttonWindowMinimize.TabIndex = 0;
            this.m_buttonWindowMinimize.Text = "Minimize";
            this.m_buttonWindowMinimize.UseVisualStyleBackColor = true;
            this.m_buttonWindowMinimize.Click += new System.EventHandler(this.buttonWindowMinimizeClick);
            // 
            // MainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1280, 800);
            this.Controls.Add(this.m_panelContent);
            this.Controls.Add(this.m_panelNavigation);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            this.MinimumSize = new System.Drawing.Size(1280, 800);
            this.Name = "MainForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Form1";
            this.m_panelNavigation.ResumeLayout(false);
            this.m_panelContent.ResumeLayout(false);
            this.m_panelTopBar.ResumeLayout(false);
            this.m_panelTopBar.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Panel m_panelNavigation;
        private System.Windows.Forms.Button m_buttonExit;
        private System.Windows.Forms.Button m_buttonHome;
        private System.Windows.Forms.Button m_buttonLog;
        private System.Windows.Forms.Button m_buttonSettings;
        private System.Windows.Forms.Panel m_panelLoad;
        private System.Windows.Forms.Panel m_panelHighlight;
        private System.Windows.Forms.Panel m_panelContent;
        private System.Windows.Forms.Panel m_panelTopBar;
        private System.Windows.Forms.Label m_labelTitle;
        private System.Windows.Forms.Button m_buttonWindowToggle;
        private System.Windows.Forms.Button m_buttonWindowMinimize;
    }
}

