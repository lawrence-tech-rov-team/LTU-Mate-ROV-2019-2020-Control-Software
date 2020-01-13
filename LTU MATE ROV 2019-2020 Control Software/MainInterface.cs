﻿using ExcelInterface.Writer;
using JoystickInput;
using LTU_MATE_ROV_2019_2020_Control_Software.Ethernet;
using LTU_MATE_ROV_2019_2020_Control_Software.InputControls;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace LTU_MATE_ROV_2019_2020_Control_Software {
	public partial class MainInterface : Form, IKeyboardListener {

		private ControllerType currentController = ControllerType.None;
		private Random rnd = new Random();
		private EthernetInterface ethernet = new EthernetInterface();
		private Stopwatch timer = new Stopwatch();
		private int speedCounter = 0;
		private bool ledState = false;
		//TODO ethernet interface usage should be moved to Robot thingy (on its own thread)

		public MainInterface() {
			InitializeComponent();
			ethernet.OnPacketReceived += RunCommand;
		}

		private void MainInterface_Load(object sender, EventArgs e) {
			RobotThread.Start();
			RobotThread.SetControllerType(currentController, this);
			this.KeyPreview = true;

			InputDataTimer.Start();
		}

		private void MainInterface_FormClosing(object sender, FormClosingEventArgs e) {
			RobotThread.RequestStop();
			//Stop other threads
			RobotThread.Stop();
			if (ethernet != null) {
				ethernet.Disconnect();
			}
		}

		private void RunCommand(UdpPacket packet) {
			switch (packet.Command) {
				case Command.Ping:
					CmdPing(packet);
					return;
				case Command.Echo:
					CmdEcho(packet);
					return;
				default: return;
			}
		}

		private void CmdPing(UdpPacket packet) {
			/*
			if(recvData.Count >= 3) {
				byte[] buffer = fillFromBuffer(3);
				if ((buffer == null) || (buffer.Length != 3)) return;
				if (buffer[0] != 0x00) return;
				else foundStart = false;
				byte checksum = (byte)((0xFF + buffer[0] + buffer[1]) & 0x7F);
				if(checksum == buffer[2]) {
					Console.WriteLine("Ping! {0}", buffer[1]);
				}
			}*/
			if (packet.Data.Length == 1) {
				Console.WriteLine("Ping! {0}", packet.Data[0]);
			}
		}

		private void CmdEcho(UdpPacket packet) {
			if (timer.IsRunning && (packet.Data.Length == 255)) {
				speedCounter++;
				if (speedCounter >= 8) {
					timer.Stop();
					Console.WriteLine("Average Time: {0} ms", timer.Elapsed.TotalMilliseconds);
				}
			} else {
				Console.WriteLine("Ehco! {0}", Encoding.UTF8.GetString(packet.Data));
			}
		}

		private void ControlsMenu_Click(object sender, EventArgs e) {
			
		}

		private void KeyboardMenu_Click(object sender, EventArgs e) {
			new KeyboardConfigForm().ShowDialog();
			RobotThread.SetControllerType(currentController, this);
		}

		private void JoystickMenu_Click(object sender, EventArgs e) {
			new JoystickConfigForm().ShowDialog();
			RobotThread.SetControllerType(currentController, this);
		}

		private void InputDataTimer_Tick(object sender, EventArgs e) {
			InputControlData data = RobotThread.GetInputData();
			if (data == null) data = new InputControlData(); 
			PowerMeter.Value = Math.Max(-1, Math.Min(1, (decimal)data.ForwardThrust));
		}

		private void ControllerTypeButton_CheckedChanged(object sender, EventArgs e) {
			if (!(sender is RadioButton)) return;
			if (((RadioButton)sender).Checked) {
				currentController = ControllerType.None;
				if (sender == KeyboardBtn) currentController = ControllerType.Keyboard;
				else if (sender == JoystickBtn) currentController = ControllerType.Joystick;

				RobotThread.SetControllerType(currentController, this);
			}
		}

		private void saveExcelToolStripMenuItem_Click(object sender, EventArgs e) {
			try {
				using(ExcelFileWriter file = ExcelFileWriter.OpenExcelApplication()) {
					if (file == null) throw new NullReferenceException("Failed to open the Excel application.");
					using (WorkbookWriter book = file.CreateNewWorkbook()) {
						if (book == null) throw new NullReferenceException("Failed to create a new Excel workbook.");
						WorksheetWriter sheet = book.GetActiveWorksheet();
						if (sheet == null) throw new NullReferenceException("Failed to find the active worksheet.");
						sheet.Name = "TestSheet1";

						int col;
						int row = WorksheetWriter.MinRow;
						for(; row <= 2; row++) { //2 rows
							for(col = WorksheetWriter.MinColumn; col <= 5; col++) { //5 cols
								sheet[row, col] = "R" + row + "C" + col;
								if (row == WorksheetWriter.MinRow) sheet.Bold(row, col);
							}
						}

						row += 2;
						col = WorksheetWriter.MinColumn;
						sheet[row, col] = "This is an extra long cell to fit.";

						sheet.AutoFitAllColumns();


						WorksheetWriter sheet2 = book.CreateNewWorksheet();
						sheet2.Name = "TestSheet2";
						sheet[WorksheetWriter.MinRow, WorksheetWriter.MinColumn] = "This is really long but isn't fitted.";

						book.Save("TestExcel" + WorkbookWriter.DEFAULT_FILE_EXTENSION);
					}
				}
			}catch(Exception) {
				MessageBox.Show("Error");
			}
		}

		private void saveCSVToolStripMenuItem_Click(object sender, EventArgs e) {
			List<string> lines = new List<string>();
			List<string> line = new List<string>();

			for(int row = 0; row < 2; row++) { //two rows
				line.Clear();
				for (int col = 0; col < 5; col++) { //5 cols
					line.Add("R" + row + "C" + col);
				}

				lines.Add(string.Join(",", line.Select(cell => "\"" + cell + "\"").ToArray()));
			}

			try {
				string path = "TestCsv.csv";
				if (File.Exists(path)) File.Delete(path);
				File.WriteAllLines(path, lines);
				return; //Return true
			} catch (Exception) {
				MessageBox.Show("Error");
			}
		}

		private void connectToolStripMenuItem_Click(object sender, EventArgs e) {
			if (ethernet.TryConnect()) {
				MessageBox.Show("Connected!");
			} else {
				MessageBox.Show("Could not connect to device.");
			}
		}

		private void disconnectToolStripMenuItem_Click(object sender, EventArgs e) {
			ethernet.Disconnect();
		}

		private void pingToolStripMenuItem_Click(object sender, EventArgs e) {
			byte num = (byte)(rnd.Next(0, 255) & 0xFF);
			if (!ethernet.Send(Command.Ping, num)) {
				MessageBox.Show("Error sending ping.");
			}
		}

		private void speedTestToolStripMenuItem_Click(object sender, EventArgs e) {
			timer.Stop();
			speedCounter = 0;
			byte[] data = new byte[255];
			rnd.NextBytes(data);

			timer.Restart();
			for (int i = 0; i < 8; i++) {
				ethernet.Send(Command.Echo, data);
			}
		}

		private void toggleLedToolStripMenuItem_Click(object sender, EventArgs e) {
			ledState = !ledState;
			ethernet.Send(Command.Led, ledState ? (byte)1 : (byte)0);
		}
	}
}