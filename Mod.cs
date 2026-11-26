using System;
using System.Collections.Generic;
using MelonLoader;
using HarmonyLib;
using Il2CppScheduleOne.Money;
using Il2CppScheduleOne.ObjectScripts;

[assembly: MelonInfo(typeof(ATMFixedDrop.Core), "Fixed Cash Drops", "1.9.0", "purplehaxttv")]
[assembly: MelonGame("TVGS", "Schedule I")]

namespace ATMFixedDrop
{
    public class Core : MelonMod
    {
        private static MelonPreferences_Category cfg;
        private static MelonPreferences_Entry<int> payoutEntry;
        private static MelonPreferences_Entry<bool> atmEntry;
        private static MelonPreferences_Entry<bool> cukeEntry;
        private static MelonPreferences_Entry<bool> recyclerEntry;

        internal static MelonLogger.Instance Log;

        // Each payout can be switched off on its own; an entry set to false leaves that one vanilla.
        internal static bool FixATM => atmEntry == null || atmEntry.Value;
        internal static bool FixCukeMachine => cukeEntry == null || cukeEntry.Value;
        internal static bool FixRecycler => recyclerEntry == null || recyclerEntry.Value;

        // Target dollars. Recycler uses this directly; ATM/Vending drop it as ~$10 bills.
        internal static int PayoutDollars => payoutEntry != null ? payoutEntry.Value : 200;

        internal static int BillCount
        {
            get { int b = PayoutDollars / 10; return b < 1 ? 1 : b; }
        }

        private static MelonPreferences_Entry<float> recyclerMultiplierEntry;
        private static MelonPreferences_Entry<float> machineMultiplierEntry;
        private static MelonPreferences_Entry<bool> verboseEntry;

        internal static float RecyclerMultiplier => recyclerMultiplierEntry != null ? recyclerMultiplierEntry.Value : 0f;
        internal static float MachineMultiplier => machineMultiplierEntry != null ? machineMultiplierEntry.Value : 0f;

        // How many $10 bills a broken ATM or Cuke machine drops when the game rolled `rolled` of them.
        internal static int MachineBills(int rolled)
        {
            float m = MachineMultiplier;
            if (m > 0f) return Math.Max(1, (int)Math.Round(rolled * m));
            return BillCount;
        }
        internal static bool Verbose => verboseEntry != null && verboseEntry.Value;

        // What a recycler pays for trash the game valued at `vanilla` dollars.
        internal static float RecyclerPayout(float vanilla)
        {
            float m = RecyclerMultiplier;
            if (m > 0f) return (float)Math.Round(vanilla * m);
            return PayoutDollars;
        }

        public override void OnInitializeMelon()
        {
            Log = LoggerInstance;
            cfg = MelonPreferences.CreateCategory("ATMFixedDrop");
            payoutEntry = cfg.CreateEntry("PayoutDollars", 200,
                description: "Flat payout, only used where a multiplier below is set to 0. Rounds down to the nearest $10 for ATMs and Cuke machines.");
            atmEntry = cfg.CreateEntry("FixATM", true, description: "Change what broken ATMs drop. false = vanilla payout.");
            cukeEntry = cfg.CreateEntry("FixCukeMachine", true, description: "Change what broken Cuke machines drop. false = vanilla payout.");
            machineMultiplierEntry = cfg.CreateEntry("MachineMultiplier", 7f,
                description: "Broken ATMs and Cuke machines drop the game's own random amount, times this. Vanilla ATMs roll $20 to $80 and Cuke machines $10 to $40, so 7 gives $140 to $560 and $70 to $280. 0 = always drop a flat PayoutDollars.");
            recyclerEntry = cfg.CreateEntry("FixRecycler", true, description: "Change what trash recyclers pay. false = vanilla payout.");
            recyclerMultiplierEntry = cfg.CreateEntry("RecyclerMultiplier", 7f,
                description: "Recyclers pay the real value of the trash put in, times this. The game values each piece at $1 to $3 (bags count by their contents). 0 = ignore the trash and pay a flat PayoutDollars per press.");
            verboseEntry = cfg.CreateEntry("VerboseLog", false, description: "Log every recycler value change (for troubleshooting).");
            string recycler = !FixRecycler ? "vanilla" : RecyclerMultiplier > 0f ? $"trash value x{RecyclerMultiplier}" : $"flat ${PayoutDollars}";
            string machine = MachineMultiplier > 0f ? $"random roll x{MachineMultiplier}" : $"flat ${PayoutDollars}";
            Log.Msg($"Fixed Cash Drops v1.9 loaded. ATMs: {(FixATM ? machine : "vanilla")}, Cuke machines: {(FixCukeMachine ? machine : "vanilla")}, recyclers: {recycler}.");
        }
    }

    // --- ATM: overwrite the break coroutine's cashDrop, once per break. ---
    [HarmonyPatch(typeof(ATM.ObjectCompilerGeneratedNPrivateSealedIEnumerator1ObjectIEnumeratorIDisposableInObATInObInObUnique), "MoveNext")]
    public static class ATM_BreakRoutine_Patch
    {
        private static readonly HashSet<IntPtr> handled = new HashSet<IntPtr>();

        [HarmonyPostfix]
        public static void Postfix(ATM.ObjectCompilerGeneratedNPrivateSealedIEnumerator1ObjectIEnumeratorIDisposableInObATInObInObUnique __instance)
        {
            if (!Core.FixATM) return;
            try
            {
                int rolled = __instance._cashDrop_5__2;
                if (rolled > 0 && handled.Add(__instance.Pointer))
                {
                    int bills = Core.MachineBills(rolled);
                    Core.Log.Msg($"[ATM] rolled ${rolled * 10}, drops ${bills * 10}");
                    __instance._cashDrop_5__2 = bills;
                    if (handled.Count > 500) handled.Clear();
                }
            }
            catch (Exception e) { Core.Log.Warning("ATM patch error: " + e.Message); }
        }
    }

    // --- Cuke machine: same mechanic as the ATM. ---
    [HarmonyPatch(typeof(VendingMachine.ObjectCompilerGeneratedNPrivateSealedIEnumerator1ObjectIEnumeratorIDisposableInObVeInObInObUnique), "MoveNext")]
    public static class Vending_BreakRoutine_Patch
    {
        private static readonly HashSet<IntPtr> handled = new HashSet<IntPtr>();

        [HarmonyPostfix]
        public static void Postfix(VendingMachine.ObjectCompilerGeneratedNPrivateSealedIEnumerator1ObjectIEnumeratorIDisposableInObVeInObInObUnique __instance)
        {
            if (!Core.FixCukeMachine) return;
            try
            {
                int rolled = __instance._cashDrop_5__2;
                if (rolled > 0 && handled.Add(__instance.Pointer))
                {
                    int bills = Core.MachineBills(rolled);
                    Core.Log.Msg($"[Cuke machine] rolled ${rolled * 10}, drops ${bills * 10}");
                    __instance._cashDrop_5__2 = bills;
                    if (handled.Count > 500) handled.Clear();
                }
            }
            catch (Exception e) { Core.Log.Warning("Vending patch error: " + e.Message); }
        }
    }

    // --- Trash recycler. The game adds up the SellValue of every piece of trash put in ($1 to $3
    // each, bags count by their contents). That total is the 'value' local of the Process coroutine,
    // and SetCashValue carries the same number to the machine's cash and its on-screen label.
    // Both are changed with the same rule, so the screen and the cash always agree. ---
    [HarmonyPatch(typeof(Recycler), nameof(Recycler.SetCashValue))]
    public static class Recycler_SetCashValue_Patch
    {
        [HarmonyPrefix]
        public static void Prefix(Recycler __instance, ref float amount)
        {
            if (!Core.FixRecycler || amount <= 0f) return;
            try
            {
                float before = amount;
                amount = Recycler_Process_Patch.PayoutFor(__instance, amount);
                if (Core.Verbose) Core.Log.Msg($"[Recycler] SetCashValue {before} -> {amount}");
            }
            catch (Exception e) { Core.Log.Warning("Recycler cash patch error: " + e.Message); }
        }
    }

    [HarmonyPatch(typeof(Recycler._Process_d__45), "MoveNext")]
    public static class Recycler_Process_Patch
    {
        private static readonly HashSet<IntPtr> handled = new HashSet<IntPtr>();

        // What the current run of each recycler really added up to, and what we turned it into.
        // Lets the two hooks recognise a number the other one already changed.
        private static readonly Dictionary<IntPtr, float> rawByRecycler = new Dictionary<IntPtr, float>();
        private static readonly Dictionary<IntPtr, float> paidByRecycler = new Dictionary<IntPtr, float>();

        internal static float PayoutFor(Recycler recycler, float value)
        {
            IntPtr key = recycler != null ? recycler.Pointer : IntPtr.Zero;
            if (paidByRecycler.TryGetValue(key, out float paid) && Math.Abs(paid - value) < 0.01f
                && rawByRecycler.TryGetValue(key, out float raw) && Math.Abs(raw - value) > 0.01f)
                return value; // already ours
            float result = Core.RecyclerPayout(value);
            rawByRecycler[key] = value;
            paidByRecycler[key] = result;
            return result;
        }

        [HarmonyPostfix]
        public static void Postfix(Recycler._Process_d__45 __instance)
        {
            if (!Core.FixRecycler) return;
            try
            {
                float rolled = __instance._value_5__2;
                if (rolled > 0f && handled.Add(__instance.Pointer))
                {
                    float pay = PayoutFor(__instance.__4__this, rolled);
                    Core.Log.Msg($"[Recycler] trash worth ${rolled} pays ${pay}");
                    __instance._value_5__2 = pay;
                    if (handled.Count > 500) handled.Clear();
                }
                else if (rolled <= 0f)
                {
                    // a new run is starting on this machine: forget the previous one
                    var rec = __instance.__4__this;
                    if (rec != null) { rawByRecycler.Remove(rec.Pointer); paidByRecycler.Remove(rec.Pointer); }
                }
            }
            catch (Exception e) { Core.Log.Warning("Recycler process patch error: " + e.Message); }
        }
    }
}
