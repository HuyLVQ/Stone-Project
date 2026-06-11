namespace Stone_Application.Forms
{
    partial class FormLogs
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
            this.m_groupBoxLogging = new System.Windows.Forms.GroupBox();
            this.m_buttonExportPDF = new System.Windows.Forms.Button();
            this.textBoxLogging = new System.Windows.Forms.RichTextBox();
            this.m_groupBoxLogging.SuspendLayout();
            this.SuspendLayout();
            // 
            // groupBoxLogging
            // 
            this.m_groupBoxLogging.Controls.Add(this.m_buttonExportPDF);
            this.m_groupBoxLogging.Controls.Add(this.textBoxLogging);
            this.m_groupBoxLogging.Dock = System.Windows.Forms.DockStyle.Fill;
            this.m_groupBoxLogging.Font = new System.Drawing.Font("Nirmala UI", 19.8F, System.Drawing.FontStyle.Bold);
            this.m_groupBoxLogging.Location = new System.Drawing.Point(12, 12);
            this.m_groupBoxLogging.Margin = new System.Windows.Forms.Padding(12);
            this.m_groupBoxLogging.Name = "groupBoxLogging";
            this.m_groupBoxLogging.Size = new System.Drawing.Size(1056, 776);
            this.m_groupBoxLogging.TabIndex = 0;
            this.m_groupBoxLogging.TabStop = false;
            this.m_groupBoxLogging.Text = "Logging";
            // 
            // buttonExportPDF
            // 
            this.m_buttonExportPDF.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.m_buttonExportPDF.Font = new System.Drawing.Font("Nirmala UI", 10.8F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.m_buttonExportPDF.Location = new System.Drawing.Point(870, 723);
            this.m_buttonExportPDF.Name = "buttonExportPDF";
            this.m_buttonExportPDF.Size = new System.Drawing.Size(155, 37);
            this.m_buttonExportPDF.TabIndex = 1;
            this.m_buttonExportPDF.Text = "Export PDF";
            this.m_buttonExportPDF.UseVisualStyleBackColor = true;
            this.m_buttonExportPDF.Click += new System.EventHandler(this.buttonExportPDFClick);
            // 
            // textBoxLogging
            // 
            this.textBoxLogging.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.textBoxLogging.Location = new System.Drawing.Point(24, 58);
            this.textBoxLogging.Name = "textBoxLogging";
            this.textBoxLogging.Size = new System.Drawing.Size(1001, 649);
            this.textBoxLogging.TabIndex = 0;
            this.textBoxLogging.Text = "";
            // 
            // FormLogs
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1080, 800);
            this.Controls.Add(this.m_groupBoxLogging);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            this.Padding = new System.Windows.Forms.Padding(12);
            this.Name = "FormLogs";
            this.Text = "FormLogs";
            this.m_groupBoxLogging.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.GroupBox m_groupBoxLogging;
        public System.Windows.Forms.RichTextBox textBoxLogging;
        private System.Windows.Forms.Button m_buttonExportPDF;
    }
}
