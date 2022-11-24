using System;

using HarmonyLib;
using UnityEngine;

using RimWorld;
using Verse;
using Verse.AI;
using System.Collections.Generic;
using System.Reflection;

namespace CookingAgriculture {
	[StaticConstructorOnStartup]
	public static class HarmonyBase {
		private static Harmony harmony = null;
		static internal Harmony instance {
			get {
				if (harmony == null)
					harmony = new Harmony("CookingAgriculture.Harmony");
				return harmony;
			}
		}

		static HarmonyBase() {
			Harmony.DEBUG = true;
			instance.PatchAll();
		}
	}
}
