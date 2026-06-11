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
            this.pictureBox = new System.Windows.Forms.PictureBox();
            this.m_groupBoxPicture.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox)).BeginInit();
            this.SuspendLayout();
            // 
            // groupBoxPicture
            // 
            this.m_groupBoxPicture.Controls.Add(this.pictureBox);
            this.m_groupBoxPicture.Dock = System.Windows.Forms.DockStyle.Fill;
            this.m_groupBoxPicture.Font = new System.Drawing.Font("Nirmala UI", 19.8F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.m_groupBoxPicture.Location = new System.Drawing.Point(12, 12);
            this.m_groupBoxPicture.Margin = new System.Windows.Forms.Padding(12);
            this.m_groupBoxPicture.Name = "groupBoxPicture";
            this.m_groupBoxPicture.Padding = new System.Windows.Forms.Padding(16);
            this.m_groupBoxPicture.Size = new System.Drawing.Size(1056, 776);
            this.m_groupBoxPicture.TabIndex = 0;
            this.m_groupBoxPicture.TabStop = false;
            this.m_groupBoxPicture.Text = "Video Stream";
            // 
            // pictureBox
            // 
            this.pictureBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pictureBox.Name = "pictureBox";
            this.pictureBox.Size = new System.Drawing.Size(1024, 714);
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
            this.Padding = new System.Windows.Forms.Padding(12);
            this.Name = "FormHome";
            this.Text = "FormHome";
            this.m_groupBoxPicture.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.GroupBox m_groupBoxPicture;
        public System.Windows.Forms.PictureBox pictureBox;
    }
}
