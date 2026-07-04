namespace Stone_Application.Forms
{
    partial class FormHome
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
            this.m_groupBoxPicture = new System.Windows.Forms.GroupBox();
            this.m_stopButton = new System.Windows.Forms.Button();
            this.m_startButton = new System.Windows.Forms.Button();
            this.pictureBox = new System.Windows.Forms.PictureBox();
            this.m_groupBoxPicture.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox)).BeginInit();
            this.SuspendLayout();
            // 
            // m_groupBoxPicture
            // 
            this.m_groupBoxPicture.Controls.Add(this.m_stopButton);
            this.m_groupBoxPicture.Controls.Add(this.m_startButton);
            this.m_groupBoxPicture.Controls.Add(this.pictureBox);
            this.m_groupBoxPicture.Dock = System.Windows.Forms.DockStyle.Fill;
            this.m_groupBoxPicture.Font = new System.Drawing.Font("Nirmala UI", 19.8F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.m_groupBoxPicture.Location = new System.Drawing.Point(12, 12);
            this.m_groupBoxPicture.Margin = new System.Windows.Forms.Padding(12);
            this.m_groupBoxPicture.Name = "m_groupBoxPicture";
            this.m_groupBoxPicture.Padding = new System.Windows.Forms.Padding(16);
            this.m_groupBoxPicture.Size = new System.Drawing.Size(1056, 776);
            this.m_groupBoxPicture.TabIndex = 0;
            this.m_groupBoxPicture.TabStop = false;
            this.m_groupBoxPicture.Text = "Video Stream";
            // 
            // m_stopButton
            // 
            this.m_stopButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.m_stopButton.Enabled = false;
            this.m_stopButton.Font = new System.Drawing.Font("Nirmala UI", 10.8F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.m_stopButton.ForeColor = System.Drawing.SystemColors.ActiveBorder;
            this.m_stopButton.Location = new System.Drawing.Point(194, 723);
            this.m_stopButton.Name = "m_stopButton";
            this.m_stopButton.Size = new System.Drawing.Size(155, 37);
            this.m_stopButton.TabIndex = 2;
            this.m_stopButton.Text = "Stop";
            this.m_stopButton.UseVisualStyleBackColor = true;
            this.m_stopButton.Click += new System.EventHandler(this.stopButtonClick);
            // 
            // m_startButton
            // 
            this.m_startButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.m_startButton.Enabled = false;
            this.m_startButton.Font = new System.Drawing.Font("Nirmala UI", 10.8F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.m_startButton.ForeColor = System.Drawing.SystemColors.ActiveBorder;
            this.m_startButton.Location = new System.Drawing.Point(24, 723);
            this.m_startButton.Name = "m_startButton";
            this.m_startButton.Size = new System.Drawing.Size(155, 37);
            this.m_startButton.TabIndex = 1;
            this.m_startButton.Text = "Start";
            this.m_startButton.UseVisualStyleBackColor = true;
            this.m_startButton.Click += new System.EventHandler(this.startButtonClick);
            // 
            // pictureBox
            // 
            this.pictureBox.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.pictureBox.Location = new System.Drawing.Point(24, 58);
            this.pictureBox.Name = "pictureBox";
            this.pictureBox.Size = new System.Drawing.Size(1001, 649);
            this.pictureBox.TabIndex = 0;
            this.pictureBox.TabStop = false;
            // 
            // FormHome
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1080, 800);
            this.Controls.Add(this.m_groupBoxPicture);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            this.Name = "FormHome";
            this.Padding = new System.Windows.Forms.Padding(12);
            this.Text = "FormHome";
            this.m_groupBoxPicture.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.GroupBox m_groupBoxPicture;
        public System.Windows.Forms.PictureBox pictureBox;
        private System.Windows.Forms.Button m_startButton;
        private System.Windows.Forms.Button m_stopButton;
    }
}
