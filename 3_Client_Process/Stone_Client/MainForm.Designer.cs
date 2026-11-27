namespace Stone_Client
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
            this.components = new System.ComponentModel.Container();
            this.splitContainerMain = new System.Windows.Forms.SplitContainer();
            this.pictureBoxStream = new System.Windows.Forms.PictureBox();
            this.listViewLog = new System.Windows.Forms.ListView();
            this.columnHeaderLevel = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.columnHeaderTime = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.columnHeaderSource = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.columnHeaderMessage = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.imageListLevels = new System.Windows.Forms.ImageList(this.components);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainerMain)).BeginInit();
            this.splitContainerMain.Panel1.SuspendLayout();
            this.splitContainerMain.Panel2.SuspendLayout();
            this.splitContainerMain.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBoxStream)).BeginInit();
            this.SuspendLayout();
            //
            // splitContainerMain
            //
            this.splitContainerMain.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainerMain.FixedPanel = System.Windows.Forms.FixedPanel.Panel2;
            this.splitContainerMain.Location = new System.Drawing.Point(0, 0);
            this.splitContainerMain.Name = "splitContainerMain";
            this.splitContainerMain.Orientation = System.Windows.Forms.Orientation.Horizontal;
            //
            // splitContainerMain.Panel1
            //
            this.splitContainerMain.Panel1.Controls.Add(this.pictureBoxStream);
            //
            // splitContainerMain.Panel2
            //
            this.splitContainerMain.Panel2.Controls.Add(this.listViewLog);
            this.splitContainerMain.Size = new System.Drawing.Size(1200, 800);
            this.splitContainerMain.SplitterDistance = 580;
            this.splitContainerMain.TabIndex = 0;
            //
            // pictureBoxStream
            //
            this.pictureBoxStream.BackColor = System.Drawing.Color.White;
            this.pictureBoxStream.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pictureBoxStream.Location = new System.Drawing.Point(0, 0);
            this.pictureBoxStream.Name = "pictureBoxStream";
            this.pictureBoxStream.Size = new System.Drawing.Size(1200, 580);
            this.pictureBoxStream.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this.pictureBoxStream.TabIndex = 0;
            this.pictureBoxStream.TabStop = false;
            //
            // listViewLog
            //
            this.listViewLog.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.columnHeaderLevel,
            this.columnHeaderTime,
            this.columnHeaderSource,
            this.columnHeaderMessage});
            this.listViewLog.Dock = System.Windows.Forms.DockStyle.Fill;
            this.listViewLog.FullRowSelect = true;
            this.listViewLog.HeaderStyle = System.Windows.Forms.ColumnHeaderStyle.Nonclickable;
            this.listViewLog.HideSelection = false;
            this.listViewLog.Location = new System.Drawing.Point(0, 0);
            this.listViewLog.Name = "listViewLog";
            this.listViewLog.Size = new System.Drawing.Size(1200, 216);
            this.listViewLog.SmallImageList = this.imageListLevels;
            this.listViewLog.TabIndex = 0;
            this.listViewLog.UseCompatibleStateImageBehavior = false;
            this.listViewLog.View = System.Windows.Forms.View.Details;
            //
            // columnHeaderLevel
            //
            this.columnHeaderLevel.Text = "Level";
            this.columnHeaderLevel.Width = 110;
            //
            // columnHeaderTime
            //
            this.columnHeaderTime.Text = "Time";
            this.columnHeaderTime.Width = 170;
            //
            // columnHeaderSource
            //
            this.columnHeaderSource.Text = "Source";
            this.columnHeaderSource.Width = 110;
            //
            // columnHeaderMessage
            //
            this.columnHeaderMessage.Text = "Message";
            this.columnHeaderMessage.Width = 780;
            //
            // imageListLevels
            //
            this.imageListLevels.ColorDepth = System.Windows.Forms.ColorDepth.Depth32Bit;
            this.imageListLevels.ImageSize = new System.Drawing.Size(16, 16);
            this.imageListLevels.TransparentColor = System.Drawing.Color.Transparent;
            //
            // MainForm
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1200, 800);
            this.Controls.Add(this.splitContainerMain);
            this.MinimumSize = new System.Drawing.Size(640, 480);
            this.Name = "MainForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Stone Client";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.MainForm_FormClosing);
            this.Load += new System.EventHandler(this.MainForm_Load);
            this.splitContainerMain.Panel1.ResumeLayout(false);
            this.splitContainerMain.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainerMain)).EndInit();
            this.splitContainerMain.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.pictureBoxStream)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.SplitContainer splitContainerMain;
        private System.Windows.Forms.PictureBox pictureBoxStream;
        private System.Windows.Forms.ListView listViewLog;
        private System.Windows.Forms.ColumnHeader columnHeaderLevel;
        private System.Windows.Forms.ColumnHeader columnHeaderTime;
        private System.Windows.Forms.ColumnHeader columnHeaderSource;
        private System.Windows.Forms.ColumnHeader columnHeaderMessage;
        private System.Windows.Forms.ImageList imageListLevels;
    }
}
