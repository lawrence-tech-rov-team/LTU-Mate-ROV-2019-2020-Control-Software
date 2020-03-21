﻿using ExcelInterface.Writer;
using JoystickInput;
using LTU_MATE_ROV_2019_2020_Control_Software.Hardware;
using LTU_MATE_ROV_2019_2020_Control_Software.Hardware.DataTypes;
using LTU_MATE_ROV_2019_2020_Control_Software.Hardware.Ethernet;
using LTU_MATE_ROV_2019_2020_Control_Software.InputControls;
using LTU_MATE_ROV_2019_2020_Control_Software.Simulator;
using LTU_MATE_ROV_2019_2020_Control_Software.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace LTU_MATE_ROV_2019_2020_Control_Software {
	public partial class MainInterface : Form, IKeyboardListener, ILogging {

		private const ThreadPriority RovThreadPriority = ThreadPriority.Normal;

		private ControllerType currentController = ControllerType.None;
		private Random rnd = new Random();
		//private EthernetInterface ethernet;// = new EthernetInterface();
		private Stopwatch timer = new Stopwatch();
		private int speedCounter = 0;
		private bool ledState = false;
		//TODO ethernet interface usage should be moved to Robot thingy (on its own thread)
		private LogWindow LogWindow = new LogWindow();

		private ROV rov;

		public MainInterface() {
			InitializeComponent();
			//ethernet.OnPacketReceived += RunCommand;
		}

		private void MainInterface_Load(object sender, EventArgs e) {
			//RobotThread.Start();
			//RobotThread.SetControllerType(currentController, this);
			this.GetLogger().AddOutput(LogWindow);
			this.KeyPreview = true;
			rov = new ROV(RovThreadPriority, new EthernetInterface()); //TODO make this null by default, let Connect() create it. Need null handling tho

			InputDataTimer.Start();
		}

		private void MainInterface_FormClosing(object sender, FormClosingEventArgs e) {
			//RobotThread.RequestStop();
			//Stop other threads
			//RobotThread.Stop();
			rov.Stop(); //TODO before disconnecting, release all servos
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
				Console.WriteLine("Ping! {0} Latency: {1} ms", packet.Data[0], timer.Elapsed.TotalMilliseconds);
			}
		}

		private void CmdEcho(UdpPacket packet) {
			if (timer.IsRunning && (packet.Data.Length == 255)) {
				speedCounter++;
				if (speedCounter >= 8) {
					timer.Stop();
					Console.WriteLine("Average Time: {0} ms or {1} bit/s", timer.Elapsed.TotalMilliseconds, (255 * 8 * 8) / timer.Elapsed.TotalSeconds);
				}
			} else {
				Console.WriteLine("Ehco! {0}", Encoding.UTF8.GetString(packet.Data.ToArray()));
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
			lock (this) {
				TestBtnMeter.Value = rov.TestButton.State;
				TestBtn2.Value = rov.TestButton2.State;

				TempLabel.Text = "Temperature: " + rov.IMU.Temperature.ToString().PadLeft(4) + "°C";
				//Vector3Data euler = rov.IMU.Euler;
				Vector3Data accel = rov.IMU.Accelerometer;

				/*if (euler != null) {
					EulerX.Text = "X: " + euler.x.ToString("0.00").PadLeft(10) + "°";
					EulerY.Text = "Y: " + euler.y.ToString("0.00").PadLeft(10) + "°";
					EulerZ.Text = "Z: " + euler.z.ToString("0.00").PadLeft(10) + "°";
				}*/

				if (accel != null) {
					AccelX.Text = "X: " + accel.x.ToString("0.00").PadLeft(10) + " m/s²";
					AccelY.Text = "Y: " + accel.y.ToString("0.00").PadLeft(10) + "m/s²";
					AccelZ.Text = "Z: " + accel.z.ToString("0.00").PadLeft(10) + "m/s²";
				}

				WaterTempLabel.Text = "Water Temp: " + rov.PressureSensor.Temperature.ToString("0.00").PadLeft(10) + "°C";
				PressureLabel.Text = "Pressure: " + rov.PressureSensor.Pressure.ToString("0.00").PadLeft(10) + " mBar";
				AltitudeLabel.Text = "Altitude: " + rov.PressureSensor.Altitude.ToString("0.00").PadLeft(10) + " m above mean sea";
				DepthLabel.Text = "Depth: " + rov.PressureSensor.Depth.ToString("0.00").PadLeft(10) + " m";

				//InputControlData data = RobotThread.GetInputData();
				//if (data == null) data = new InputControlData(); 
				//PowerMeter.Value = Math.Max(-1, Math.Min(1, (decimal)data.ForwardThrust));
			}
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
			if((rov == null) || (rov.IsSimulator)) {
				rov?.Disconnect();
				rov = new ROV(RovThreadPriority, new EthernetInterface());
			}

			if (rov.Connect()) {
				MessageBox.Show("Connected!");
			} else {
				MessageBox.Show("Could not connect to device.");
			}
		}

		private void disconnectToolStripMenuItem_Click(object sender, EventArgs e) {
			rov.Disconnect();
		}

		private void pingToolStripMenuItem_Click(object sender, EventArgs e) {
			/*byte num = (byte)(rnd.Next(0, 255) & 0xFF);
			timer.Restart();
			if (!ethernet.Send(Command.Ping, num)) {
				MessageBox.Show("Error sending ping.");
			}*/
			//TODO finish button code
		}

		private void speedTestToolStripMenuItem_Click(object sender, EventArgs e) {
			/*timer.Stop();
			speedCounter = 0;
			byte[] data = new byte[255];
			rnd.NextBytes(data);

			timer.Restart();
			for (int i = 0; i < 8; i++) {
				ethernet.Send(Command.Echo, data);
			}*/
			//TODO finish button code
		}

		private void toggleLedToolStripMenuItem_Click(object sender, EventArgs e) {
			//ledState = !ledState;
			//ethernet.Send(Command.Led, ledState ? (byte)1 : (byte)0);
			//TODO led toggle
		}

		private void logToolStripMenuItem_Click(object sender, EventArgs e) {
			LogWindow.Show();
		}

		private void button1_Click(object sender, EventArgs e) {
			this.Log(CustomLogger.LogLevel.Warn, "I\'m warning you!");
		}

		private void button2_Click(object sender, EventArgs e) {
			this.Log(CustomLogger.LogLevel.Info, "I am not the info desk.");
		}

		private void button3_Click(object sender, EventArgs e) {
			this.Log(CustomLogger.LogLevel.Debug, "Bugs everywhere!");
		}

		private void hardwarePingToolStripMenuItem_Click(object sender, EventArgs e) {
			long? timeMs = rov.Ping(1000);
			if(timeMs == null) {
				MessageBox.Show("Ping failed.");
			} else {
				MessageBox.Show("Ping: " + (long)timeMs + " ms");
			}
		}

		private void simulatorToolStripMenuItem_Click(object sender, EventArgs e) {
			lock (this) {
				rov?.Disconnect();

				RobotSimulator sim = new RobotSimulator();
				rov = new ROV(RovThreadPriority, sim);
				rov.Connect();
			}
		}

		private void PosTrackBar_Scroll(object sender, EventArgs e) {
			//byte val = (byte)PosTrackBar.Value;
			//PosLabel.Text = val.ToString();
			//rov.ServoA1.SetPosition(val);
			PosNum.Value = PosTrackBar.Value;
		}

		private void EnableServo_CheckedChanged(object sender, EventArgs e) {
			rov.ServoA1.Enable = EnableServo.Checked;
		}

		private void PosNum_ValueChanged(object sender, EventArgs e) {
			ushort us = 0;

			try {
				us = decimal.ToUInt16(PosNum.Value);

			} catch (Exception) {
				return;
			}

			if (us < 0) us = 0;
			else if (us > 3000) us = 3000;
			rov.ServoA1.Pulse = us;
		}

		private void EnableServo2_CheckedChanged(object sender, EventArgs e) {
			rov.ServoC1.Enable = EnableServo2.Checked;
		}

		private void PosTrackBar2_Scroll(object sender, EventArgs e) {
			PosNum2.Value = PosTrackBar2.Value;
		}

		private void PosNum2_ValueChanged(object sender, EventArgs e) {
			ushort us = 0;

			try {
				us = decimal.ToUInt16(PosNum2.Value);

			} catch (Exception) {
				return;
			}

			if (us < 0) us = 0;
			else if (us > 3000) us = 3000;
			rov.ServoC1.Pulse = us;
		}

		private void LedBtn_MouseDown(object sender, MouseEventArgs e) {
			rov.LED.Enabled = true;
		}

		private void LedBtn_MouseUp(object sender, MouseEventArgs e) {
			rov.LED.Enabled = false;
		}
	}
}
