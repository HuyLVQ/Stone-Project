namespace Stone_Application.Forms
{
    partial class FormLogs
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
            this.groupBoxLogging = new System.Windows.Forms.GroupBox();
            this.buttonExportPDF = new System.Windows.Forms.Button();
            this.textBoxLogging = new System.Windows.Forms.RichTextBox();
            this.groupBoxLogging.SuspendLayout();
            this.SuspendLayout();
            // 
            // groupBoxLogging
            // 
            this.groupBoxLogging.Controls.Add(this.buttonExportPDF);
            this.groupBoxLogging.Controls.Add(this.textBoxLogging);
            this.groupBoxLogging.Font = new System.Drawing.Font("Nirmala UI", 19.8F, System.Drawing.FontStyle.Bold);
            this.groupBoxLogging.Location = new System.Drawing.Point(12, 12);
            this.groupBoxLogging.Name = "groupBoxLogging";
            this.groupBoxLogging.Size = new System.Drawing.Size(1056, 776);
            this.groupBoxLogging.TabIndex = 0;
            this.groupBoxLogging.TabStop = false;
            this.groupBoxLogging.Text = "Logging";
            // 
            // buttonExportPDF
            // 
            this.buttonExportPDF.Font = new System.Drawing.Font("Nirmala UI", 10.8F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.buttonExportPDF.Location = new System.Drawing.Point(855, 709);
            this.buttonExportPDF.Name = "buttonExportPDF";
            this.buttonExportPDF.Size = new System.Drawing.Size(155, 37);
            this.buttonExportPDF.TabIndex = 1;
            this.buttonExportPDF.Text = "Export PDF";
            this.buttonExportPDF.UseVisualStyleBackColor = true;
            this.buttonExportPDF.Click += new System.EventHandler(this.buttonExportPDFClick);
            // 
            // textBoxLogging
            // 
            this.textBoxLogging.Location = new System.Drawing.Point(50, 75);
            this.textBoxLogging.Name = "textBoxLogging";
            this.textBoxLogging.Size = new System.Drawing.Size(960, 600);
            this.textBoxLogging.TabIndex = 0;
            this.textBoxLogging.Text = "";
            // 
            // FormLogs
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1080, 800);
            this.Controls.Add(this.groupBoxLogging);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            this.Name = "FormLogs";
            this.Text = "FormLogs";
            this.groupBoxLogging.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.GroupBox groupBoxLogging;
        public System.Windows.Forms.RichTextBox textBoxLogging;
        private System.Windows.Forms.Button buttonExportPDF;
    }
}