namespace XPlaneMapReceiver
{
    partial class Form1
    {
        /// <summary>
        ///Gerekli tasarımcı değişkeni.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///Kullanılan tüm kaynakları temizleyin.
        /// </summary>
        ///<param name="disposing">yönetilen kaynaklar dispose edilmeliyse doğru; aksi halde yanlış.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer üretilen kod

        /// <summary>
        /// Tasarımcı desteği için gerekli metot - bu metodun 
        ///içeriğini kod düzenleyici ile değiştirmeyin.
        /// </summary>
        private void InitializeComponent()
        {
            this.textBox1 = new System.Windows.Forms.TextBox();
            this.comboBoxDeparture = new System.Windows.Forms.ComboBox();
            this.comboBoxArrival = new System.Windows.Forms.ComboBox();
            this.StartFlight = new System.Windows.Forms.Button();
            this.btnStartRecording = new System.Windows.Forms.Button();
            this.btnStopRecording = new System.Windows.Forms.Button();
            this.btnOpenRecording = new System.Windows.Forms.Button();
            this.btnReplay = new System.Windows.Forms.Button();
            this.btnPauseReplay = new System.Windows.Forms.Button();
            this.btnResumeReplay = new System.Windows.Forms.Button();
            this.trackBarReplaySpeed = new System.Windows.Forms.TrackBar();
            this.lblReplaySpeed = new System.Windows.Forms.Label();
            this.btnForward = new System.Windows.Forms.Button();
            this.btnRewind = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)(this.trackBarReplaySpeed)).BeginInit();
            this.SuspendLayout();
            // 
            // textBox1
            // 
            this.textBox1.Location = new System.Drawing.Point(22, 340);
            this.textBox1.Multiline = true;
            this.textBox1.Name = "textBox1";
            this.textBox1.ReadOnly = true;
            this.textBox1.Size = new System.Drawing.Size(284, 143);
            this.textBox1.TabIndex = 0;
            // 
            // comboBoxDeparture
            // 
            this.comboBoxDeparture.AutoCompleteMode = System.Windows.Forms.AutoCompleteMode.Append;
            this.comboBoxDeparture.AutoCompleteSource = System.Windows.Forms.AutoCompleteSource.ListItems;
            this.comboBoxDeparture.FormattingEnabled = true;
            this.comboBoxDeparture.Location = new System.Drawing.Point(22, 163);
            this.comboBoxDeparture.Name = "comboBoxDeparture";
            this.comboBoxDeparture.Size = new System.Drawing.Size(284, 21);
            this.comboBoxDeparture.TabIndex = 1;
            this.comboBoxDeparture.Text = "Departure";
            this.comboBoxDeparture.SelectedIndexChanged += new System.EventHandler(this.comboBoxDeparture_SelectedIndexChanged);
            // 
            // comboBoxArrival
            // 
            this.comboBoxArrival.AutoCompleteMode = System.Windows.Forms.AutoCompleteMode.Append;
            this.comboBoxArrival.AutoCompleteSource = System.Windows.Forms.AutoCompleteSource.ListItems;
            this.comboBoxArrival.FormattingEnabled = true;
            this.comboBoxArrival.Location = new System.Drawing.Point(22, 233);
            this.comboBoxArrival.Name = "comboBoxArrival";
            this.comboBoxArrival.Size = new System.Drawing.Size(284, 21);
            this.comboBoxArrival.TabIndex = 2;
            this.comboBoxArrival.Text = "Arrival";
            this.comboBoxArrival.SelectedIndexChanged += new System.EventHandler(this.comboBoxArrival_SelectedIndexChanged);
            // 
            // StartFlight
            // 
            this.StartFlight.Location = new System.Drawing.Point(22, 279);
            this.StartFlight.Name = "StartFlight";
            this.StartFlight.Size = new System.Drawing.Size(75, 36);
            this.StartFlight.TabIndex = 3;
            this.StartFlight.Text = "Start Flight";
            this.StartFlight.UseVisualStyleBackColor = true;
            this.StartFlight.Click += new System.EventHandler(this.StartFlightButton_Click);
            // 
            // btnStartRecording
            // 
            this.btnStartRecording.Location = new System.Drawing.Point(103, 279);
            this.btnStartRecording.Name = "btnStartRecording";
            this.btnStartRecording.Size = new System.Drawing.Size(75, 36);
            this.btnStartRecording.TabIndex = 4;
            this.btnStartRecording.Text = "Start Record";
            this.btnStartRecording.UseVisualStyleBackColor = true;
            // 
            // btnStopRecording
            // 
            this.btnStopRecording.Location = new System.Drawing.Point(184, 279);
            this.btnStopRecording.Name = "btnStopRecording";
            this.btnStopRecording.Size = new System.Drawing.Size(75, 36);
            this.btnStopRecording.TabIndex = 5;
            this.btnStopRecording.Text = "Save Record";
            this.btnStopRecording.UseVisualStyleBackColor = true;
            // 
            // btnOpenRecording
            // 
            this.btnOpenRecording.Location = new System.Drawing.Point(22, 507);
            this.btnOpenRecording.Name = "btnOpenRecording";
            this.btnOpenRecording.Size = new System.Drawing.Size(75, 36);
            this.btnOpenRecording.TabIndex = 6;
            this.btnOpenRecording.Text = "Open Record";
            this.btnOpenRecording.UseVisualStyleBackColor = true;
            // 
            // btnReplay
            // 
            this.btnReplay.Location = new System.Drawing.Point(103, 507);
            this.btnReplay.Name = "btnReplay";
            this.btnReplay.Size = new System.Drawing.Size(75, 36);
            this.btnReplay.TabIndex = 7;
            this.btnReplay.Text = "Replay Record";
            this.btnReplay.UseVisualStyleBackColor = true;
            // 
            // btnPauseReplay
            // 
            this.btnPauseReplay.Location = new System.Drawing.Point(22, 549);
            this.btnPauseReplay.Name = "btnPauseReplay";
            this.btnPauseReplay.Size = new System.Drawing.Size(75, 36);
            this.btnPauseReplay.TabIndex = 8;
            this.btnPauseReplay.Text = "Pause Replay";
            this.btnPauseReplay.UseVisualStyleBackColor = true;
            // 
            // btnResumeReplay
            // 
            this.btnResumeReplay.Location = new System.Drawing.Point(103, 549);
            this.btnResumeReplay.Name = "btnResumeReplay";
            this.btnResumeReplay.Size = new System.Drawing.Size(75, 36);
            this.btnResumeReplay.TabIndex = 9;
            this.btnResumeReplay.Text = "Resume Replay";
            this.btnResumeReplay.UseVisualStyleBackColor = true;
            // 
            // trackBarReplaySpeed
            // 
            this.trackBarReplaySpeed.Location = new System.Drawing.Point(202, 507);
            this.trackBarReplaySpeed.Maximum = 99;
            this.trackBarReplaySpeed.Name = "trackBarReplaySpeed";
            this.trackBarReplaySpeed.Size = new System.Drawing.Size(104, 45);
            this.trackBarReplaySpeed.TabIndex = 10;
            // 
            // lblReplaySpeed
            // 
            this.lblReplaySpeed.AutoSize = true;
            this.lblReplaySpeed.ImageAlign = System.Drawing.ContentAlignment.TopLeft;
            this.lblReplaySpeed.Location = new System.Drawing.Point(239, 491);
            this.lblReplaySpeed.Name = "lblReplaySpeed";
            this.lblReplaySpeed.Size = new System.Drawing.Size(52, 13);
            this.lblReplaySpeed.TabIndex = 11;
            this.lblReplaySpeed.Text = "Speed (x)";
            this.lblReplaySpeed.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // btnForward
            // 
            this.btnForward.Location = new System.Drawing.Point(197, 549);
            this.btnForward.Name = "btnForward";
            this.btnForward.Size = new System.Drawing.Size(62, 23);
            this.btnForward.TabIndex = 12;
            this.btnForward.Text = "Forward";
            this.btnForward.UseVisualStyleBackColor = true;
            // 
            // btnRewind
            // 
            this.btnRewind.Location = new System.Drawing.Point(197, 579);
            this.btnRewind.Name = "btnRewind";
            this.btnRewind.Size = new System.Drawing.Size(62, 24);
            this.btnRewind.TabIndex = 13;
            this.btnRewind.Text = "Rewind";
            this.btnRewind.UseVisualStyleBackColor = true;
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1525, 755);
            this.Controls.Add(this.btnRewind);
            this.Controls.Add(this.btnForward);
            this.Controls.Add(this.lblReplaySpeed);
            this.Controls.Add(this.trackBarReplaySpeed);
            this.Controls.Add(this.btnResumeReplay);
            this.Controls.Add(this.btnPauseReplay);
            this.Controls.Add(this.btnReplay);
            this.Controls.Add(this.btnOpenRecording);
            this.Controls.Add(this.btnStopRecording);
            this.Controls.Add(this.btnStartRecording);
            this.Controls.Add(this.StartFlight);
            this.Controls.Add(this.comboBoxArrival);
            this.Controls.Add(this.comboBoxDeparture);
            this.Controls.Add(this.textBox1);
            this.Name = "Form1";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Form1";
            this.WindowState = System.Windows.Forms.FormWindowState.Maximized;
            ((System.ComponentModel.ISupportInitialize)(this.trackBarReplaySpeed)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.TextBox textBox1;
        private System.Windows.Forms.ComboBox comboBoxDeparture;
        private System.Windows.Forms.ComboBox comboBoxArrival;
        private System.Windows.Forms.Button StartFlight;
        private System.Windows.Forms.Button btnStartRecording;
        private System.Windows.Forms.Button btnStopRecording;
        private System.Windows.Forms.Button btnOpenRecording;
        private System.Windows.Forms.Button btnReplay;
        private System.Windows.Forms.Button btnPauseReplay;
        private System.Windows.Forms.Button btnResumeReplay;
        private System.Windows.Forms.TrackBar trackBarReplaySpeed;
        private System.Windows.Forms.Label lblReplaySpeed;
        private System.Windows.Forms.Button btnForward;
        private System.Windows.Forms.Button btnRewind;
        // Zoom butonları ve designer'daki webView21 kaldırıldı
    }
}

