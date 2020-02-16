﻿using LTU_MATE_ROV_2019_2020_Control_Software.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LTU_MATE_ROV_2019_2020_Control_Software.Hardware.Ethernet {
	public abstract class IEthernetLayer {

		private volatile bool connected = false;
		public bool Connected { get => connected; protected set => connected = value; }

		public delegate void NewPacketHandler(UdpPacket packet);
		public event NewPacketHandler OnPacketReceived;

		~IEthernetLayer() {
			Disconnect();
		}

		public void Disconnect() {
			lock (this) {
				Connected = false;
				Close();
			}
		}

		protected abstract void Close();

		public bool Connect() {
			lock (this) {
				Disconnect();
				if (TryConnect()) {
					Connected = true;
					return true;
				} else {
					Disconnect();
					return false;
				}
			}
		}

		protected abstract bool TryConnect();

		public bool Send(UdpPacket packet) {
			lock (this) {
				if (Connected) {
					return SendBytes(packet.AllBytes);
				}
			}
			return false;
		}

		public bool Send(Command command, ByteArray data) {
			return Send(new UdpPacket(command, data));
		}

		public bool Send(Command command, params byte[] data) {
			return Send(new UdpPacket(command, data));
		}

		public bool Send(Command command, string message, bool clip = true) {
			if (clip || (message.Length <= UdpPacket.MAX_LENGTH))
				return Send(new UdpPacket(command, Encoding.UTF8.GetBytes(message)));
			else {
				bool result = Send(new UdpPacket(command, Encoding.UTF8.GetBytes(message.Substring(0, UdpPacket.MAX_LENGTH))));
				if (!result) return false;
				return Send(command, message.Substring(UdpPacket.MAX_LENGTH), clip);
			}
		}

		protected abstract bool SendBytes(byte[] bytes);

		public abstract long? Ping(int timeoutMs = 3000);

		protected void InvokePacketReceived(UdpPacket packet) {
			if (packet != null) OnPacketReceived?.Invoke(packet);
		}

	}
}