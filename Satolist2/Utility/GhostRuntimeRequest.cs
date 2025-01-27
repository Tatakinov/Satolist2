﻿using Satolist2.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Satolist2.Utility
{
	public static class GhostRuntimeRequest
	{
		//SHIORIリロードスクリプトでリロードを行う
		public static void ReloadShiori(GhostModel ghost, string message = "SHIORIリロード。")
		{
			Core.LogMessage.AddLog("SHIORIをリロードします。");

			string script = @"\0\![reload,shiori]\![quicksession,true]" + message;
			Satorite.SendSSTP(ghost, script, true, true);
		}

	}
}
