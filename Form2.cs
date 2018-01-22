using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;

namespace PhotoCanon
{
    public partial class Form2 : Form
    {
        public Form2()
        {
            InitializeComponent();

            string line;
            int i = 0;
            string[] Flines = new string[4];

            StreamReader file = new StreamReader("Setting.txt");

            while ((line = file.ReadLine()) != null)
            {
                Flines[i] = line;
                i++;
            }

            file.Close();
            textBox1.Text = Flines[0];
            textBox2.Text = Flines[1];
            textBox3.Text = Flines[2];
            textBox4.Text = Flines[3];
        }

        private void button2_Click(object sender, EventArgs e)
        {
            string[] Flines = { textBox1.Text, textBox2.Text, textBox3.Text, textBox4.Text };
            File.WriteAllLines("Setting.txt", Flines);
            this.Close();
        }

        private void button3_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (Directory.Exists(textBox4.Text)) folderBrowserDialog1.SelectedPath = textBox4.Text;
            if (folderBrowserDialog1.ShowDialog() == DialogResult.OK)
            {
                textBox4.Text = folderBrowserDialog1.SelectedPath;
            }
        }

        private void Form2_Load(object sender, EventArgs e)
        {

        }
    }
}
