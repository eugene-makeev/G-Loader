using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO.Ports;

namespace G_loader
{
    public partial class Form1 : Form
    {
        SerialPort port = new SerialPort();

        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            // populate list of com-ports
            string[] enableComPorts = SerialPort.GetPortNames();
            foreach (string port in enableComPorts)
            {
                comboBox1.Items.Add(port);
            }

            if (comboBox1.Items.Count != 0)
            {
                comboBox1.SelectedIndex = 0;
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (Connect.Text == "Подключить")
            {
                port.PortName = comboBox1.GetItemText(comboBox1.SelectedItem);
                port.BaudRate = 115200;
                port.Parity = Parity.None;
                port.StopBits = StopBits.One;
                port.DataBits = 8;
                port.Handshake = Handshake.None;
                port.RtsEnable = false;
                try
                {
                    port.Open();
                    Connect.Text = "Отключить";
                    comboBox1.Enabled = false;
                    FwLoad.Enabled = GcodeLoad.Enabled = true;
                }
                catch
                {
                    string message = "Порт уже используется другой программой";
                    string title = "Ошибка подключения";
                    MessageBoxButtons buttons = MessageBoxButtons.OK;
                    MessageBox.Show(message, title, buttons, MessageBoxIcon.Warning);
                }
            }
            else
            {
                Connect.Text = "Подключить";
                comboBox1.Enabled = true;
                FwLoad.Enabled = GcodeLoad.Enabled = false;
                port.Close();
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            OpenFileDialog openDialog = new OpenFileDialog();
            openDialog.Filter = "All files(*.*)|*.*";
            if (openDialog.ShowDialog() != DialogResult.Cancel)
            {
                string filename = openDialog.FileName;
                string fileText = System.IO.File.ReadAllText(filename);

                // send file over COM port
            }

        }

        private void button3_Click(object sender, EventArgs e)
        {
            OpenFileDialog openDialog = new OpenFileDialog();
            openDialog.Filter = "Bin files(*.bin)|*.bin";
            if (openDialog.ShowDialog() != DialogResult.Cancel)
            {
                string filename = openDialog.FileName;
                string fileText = System.IO.File.ReadAllText(filename);

                // send file over COM port
            }
        }
    }
}
