﻿using CustomLogger;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LTU_MATE_ROV_2019_2020_Control_Software {
	public interface ILogging { //TODO logging isn't being utilized
	}

	public static class ILoggingExtensions {
		public static Logger Logger; 

		public static void Log(this ILogging obj, LogLevel level, string msg) {
			Logger.Log(level, msg);
		}

		public static Logger GetLogger(this ILogging obj) {
			return Logger;
		}
	}
}
