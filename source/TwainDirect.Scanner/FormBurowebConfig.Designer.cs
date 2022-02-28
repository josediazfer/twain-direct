
namespace TwainDirect.Scanner
{
    partial class FormBurowebConfig
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
            this.m_textBurowebURL = new System.Windows.Forms.TextBox();
            this.label1 = new System.Windows.Forms.Label();
            this.m_buttonConfigure = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // m_textBurowebURL
            // 
            this.m_textBurowebURL.Location = new System.Drawing.Point(12, 38);
            this.m_textBurowebURL.Name = "m_textBurowebURL";
            this.m_textBurowebURL.Size = new System.Drawing.Size(446, 20);
            this.m_textBurowebURL.TabIndex = 0;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(9, 9);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(266, 13);
            this.label1.TabIndex = 1;
            this.label1.Text = "URL de buroweb (ejemplo http://host:puerto/buroweb)";
            // 
            // m_buttonConfigure
            // 
            this.m_buttonConfigure.Location = new System.Drawing.Point(12, 70);
            this.m_buttonConfigure.Name = "m_buttonConfigure";
            this.m_buttonConfigure.Size = new System.Drawing.Size(170, 24);
            this.m_buttonConfigure.TabIndex = 2;
            this.m_buttonConfigure.Text = "Sincronizar";
            this.m_buttonConfigure.UseVisualStyleBackColor = true;
            this.m_buttonConfigure.Click += new System.EventHandler(this.m_buttonConfigure_Click);
            // 
            // FormBurowebConfig
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(480, 118);
            this.Controls.Add(this.m_buttonConfigure);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.m_textBurowebURL);
            this.Name = "FormBurowebConfig";
            this.Text = "Sincronizar la configuracion local a través de buroweb";
            this.Load += new System.EventHandler(this.FormBurowebConfig_Load);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.TextBox m_textBurowebURL;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Button m_buttonConfigure;
    }
}