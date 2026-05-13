using System.Windows.Forms;
using Stone_Application.IPC;

namespace Stone_Application.Forms
{
    partial class FormSettings
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
            this.groupBoxPLC = new System.Windows.Forms.GroupBox();
            this.comboBoxPort = new System.Windows.Forms.ComboBox();
            this.buttonModbus = new System.Windows.Forms.Button();
            this.groupBoxCamera = new System.Windows.Forms.GroupBox();
            this.buttonCamera = new System.Windows.Forms.Button();
            this.groupBoxAI = new System.Windows.Forms.GroupBox();
            this.buttonAI = new System.Windows.Forms.Button();
            this.groupBoxPLC.SuspendLayout();
            this.groupBoxCamera.SuspendLayout();
            this.groupBoxAI.SuspendLayout();
            this.SuspendLayout();
            // 
            // groupBoxPLC
            // 
            this.groupBoxPLC.Controls.Add(this.comboBoxPort);
            this.groupBoxPLC.Controls.Add(this.buttonModbus);
            this.groupBoxPLC.Font = new System.Drawing.Font("Nirmala UI", 10.2F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.groupBoxPLC.Location = new System.Drawing.Point(13, 13);
            this.groupBoxPLC.Name = "groupBoxPLC";
            this.groupBoxPLC.Size = new System.Drawing.Size(200, 383);
            this.groupBoxPLC.TabIndex = 0;
            this.groupBoxPLC.TabStop = false;
            this.groupBoxPLC.Text = "PLC Connection";
            // 
            // comboBoxPort
            // 
            this.comboBoxPort.Font = new System.Drawing.Font("Nirmala UI", 7.8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.comboBoxPort.FormattingEnabled = true;
            this.comboBoxPort.Location = new System.Drawing.Point(7, 68);
            this.comboBoxPort.Name = "comboBoxPort";
            this.comboBoxPort.Size = new System.Drawing.Size(187, 25);
            this.comboBoxPort.TabIndex = 1;
            this.comboBoxPort.Click += new System.EventHandler(this.comboBoxPortClick);
            // 
            // buttonModbus
            // 
            this.buttonModbus.ForeColor = System.Drawing.SystemColors.ControlText;
            this.buttonModbus.Location = new System.Drawing.Point(8, 331);
            this.buttonModbus.Name = "buttonModbus";
            this.buttonModbus.Size = new System.Drawing.Size(187, 46);
            this.buttonModbus.TabIndex = 0;
            this.buttonModbus.Text = "Connect PLC";
            this.buttonModbus.UseVisualStyleBackColor = true;
            this.buttonModbus.Click += new System.EventHandler(this.buttonPLC_Click);
            // 
            // groupBoxCamera
            // 
            this.groupBoxCamera.Controls.Add(this.buttonCamera);
            this.groupBoxCamera.Font = new System.Drawing.Font("Nirmala UI", 10.2F, System.Drawing.FontStyle.Bold);
            this.groupBoxCamera.Location = new System.Drawing.Point(13, 402);
            this.groupBoxCamera.Name = "groupBoxCamera";
            this.groupBoxCamera.Size = new System.Drawing.Size(200, 120);
            this.groupBoxCamera.TabIndex = 1;
            this.groupBoxCamera.TabStop = false;
            this.groupBoxCamera.Text = "Camera Connection";
            // 
            // buttonCamera
            // 
            this.buttonCamera.Location = new System.Drawing.Point(7, 68);
            this.buttonCamera.Name = "buttonCamera";
            this.buttonCamera.Size = new System.Drawing.Size(187, 46);
            this.buttonCamera.TabIndex = 3;
            this.buttonCamera.Text = "Connect Camera";
            this.buttonCamera.UseVisualStyleBackColor = true;
            this.buttonCamera.Click += new System.EventHandler(this.buttonCameraClick);
            // 
            // groupBoxAI
            // 
            this.groupBoxAI.Controls.Add(this.buttonAI);
            this.groupBoxAI.Font = new System.Drawing.Font("Nirmala UI", 10.2F, System.Drawing.FontStyle.Bold);
            this.groupBoxAI.Location = new System.Drawing.Point(13, 528);
            this.groupBoxAI.Name = "groupBoxAI";
            this.groupBoxAI.Size = new System.Drawing.Size(200, 120);
            this.groupBoxAI.TabIndex = 2;
            this.groupBoxAI.TabStop = false;
            this.groupBoxAI.Text = "AI Initialization";
            // 
            // buttonAI
            // 
            this.buttonAI.ForeColor = System.Drawing.SystemColors.ButtonShadow;
            this.buttonAI.Location = new System.Drawing.Point(8, 68);
            this.buttonAI.Name = "buttonAI";
            this.buttonAI.Size = new System.Drawing.Size(187, 46);
            this.buttonAI.TabIndex = 4;
            this.buttonAI.Text = "Connect AI";
            this.buttonAI.UseVisualStyleBackColor = true;
            this.buttonAI.Click += new System.EventHandler(this.buttonAIClick);
            // 
            // FormSettings
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1080, 800);
            this.Controls.Add(this.groupBoxAI);
            this.Controls.Add(this.groupBoxCamera);
            this.Controls.Add(this.groupBoxPLC);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            this.Name = "FormSettings";
            this.Text = "FormSettings";
            this.groupBoxPLC.ResumeLayout(false);
            this.groupBoxCamera.ResumeLayout(false);
            this.groupBoxAI.ResumeLayout(false);
            this.ResumeLayout(false);

        }
        public static void CleanupAllResources()
        {
            // Dispose IPC resources
            IPCServices.IPCCleanUp();

            // Dispose camera if needed
            if (Common.camera != null)
            {
                Common.camera.cameraClose();
                Common.camera = null;
            }

            // Dispose Modbus client if needed
            if (Common.modbusClient != null)
            {
                Common.modbusClient.Disconnect();
                Common.modbusClient = null;
            }
        }
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            CleanupAllResources();
            base.OnFormClosing(e);
        }

        #endregion

        private System.Windows.Forms.GroupBox groupBoxPLC;
        private System.Windows.Forms.GroupBox groupBoxCamera;
        private System.Windows.Forms.GroupBox groupBoxAI;
        private System.Windows.Forms.Button buttonModbus;
        private System.Windows.Forms.Button buttonCamera;
        private System.Windows.Forms.Button buttonAI;
        private System.Windows.Forms.ComboBox comboBoxPort;
    }
}