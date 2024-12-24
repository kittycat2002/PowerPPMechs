using aRandomKiwi.PPP;
using HarmonyLib;
using RimWorld;
using System.Reflection;
using System.Reflection.Emit;
using Verse;

namespace PowerPPMechs
{
    [StaticConstructorOnStartup]
    class PowerPPMechs
    {
        internal static Type? utils;
        internal static Type? gc_ppp;
        static PowerPPMechs()
        {
            utils = GenTypes.GetTypeInAnyAssembly("aRandomKiwi.PPP.Utils");
            gc_ppp = GenTypes.GetTypeInAnyAssembly("aRandomKiwi.PPP.GC_PPP");
            Harmony harmony = new("xyz.nekogaming.cat2002.PowerPPMechs");
            harmony.PatchAll();
        }
    }

    [HarmonyPatch]
    public static class Utils_getConsumedPowerByPawn_Patch
    {
        public static MethodBase TargetMethod()
        {
            return PowerPPMechs.utils.Method("getConsumedPowerByPawn");
        }
        public static bool Prefix(Pawn pawn, ref int __result)
        {
            if (pawn.IsColonyMech)
            {
                Need_MechEnergy need = pawn.needs.energy;
                if (need != null)
                {
                    float maxEnergy = need.MaxLevel;
                    float usageFactor = pawn.GetStatValue(StatDefOf.MechEnergyUsageFactor);
                    float daysDrain = maxEnergy / (10 * usageFactor);
                    float daysFill = maxEnergy / 50;
                    float wastepacks = pawn.GetStatValue(StatDefOf.WastepacksPerRecharge);
                    float wastepacksPerDay = wastepacks / (daysDrain + daysFill);
                    float chargeTimePercentage = daysFill / (daysDrain + daysFill);
                    __result = (int)((wastepacksPerDay + chargeTimePercentage) * 200); // a mechanoid charger uses 200W, and it takes 200W to atomize a wastepack
                    return false;
                }
            }
            return true;
        }
    }
    [HarmonyPatch]
    public static class Utils_throwChargingMote_Patch
    {
        private static MethodInfo? throwMote;
        private static MethodInfo ThrowMote
        {
            get
            {
                if (throwMote == null)
                {
                    throwMote = PowerPPMechs.utils.Method("throwMote");
                }
                return throwMote;
            }
        }
        public static MethodBase TargetMethod()
        {
            return PowerPPMechs.utils.Method("throwChargingMote");
        }
        public static bool Prefix(Pawn cp)
        {
            if (cp.IsColonyMech)
            {
                return false;
            }
            return true;
        }
    }
    [HarmonyPatch]
    public static class Utils_checkDisconnectedFromLWPNAndroid_Patch
    {
        public static MethodBase TargetMethod()
        {
            return PowerPPMechs.gc_ppp.Method(nameof(GC_PPP.checkDisconnectedFromLWPNAndroid));
        }
        private static readonly MethodInfo getPercent = AccessTools.PropertyGetter(typeof(Need), nameof(Need.CurLevelPercentage));

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            CodeMatcher codeMatcher = new(instructions, generator);
            codeMatcher.MatchStartForward(CodeMatch.LoadsLocal(name: "V_8"), CodeMatch.Calls(PowerPPMechs.utils.Method("IsMiscRobot")));
            codeMatcher.ThrowIfInvalid("Failed to find IsMiscRobot call.");
            codeMatcher.CreateLabel(out Label label1);
            codeMatcher.DefineLabel(out Label label2);
            codeMatcher.DefineLabel(out Label label3);
            codeMatcher.InsertAndAdvance([
                CodeInstruction.LoadLocal(((LocalBuilder)codeMatcher.Instruction.operand).LocalIndex),
                new CodeInstruction(OpCodes.Callvirt, AccessTools.PropertyGetter(typeof(Pawn), nameof(Pawn.IsColonyMech))),
                new CodeInstruction(OpCodes.Brfalse, label1),
                new CodeInstruction(OpCodes.Ldloc_S, codeMatcher.Instruction.operand),
                CodeInstruction.LoadField(typeof(Pawn), nameof(Pawn.needs)),
                CodeInstruction.LoadField(typeof(Pawn_NeedsTracker), nameof(Pawn_NeedsTracker.energy)),
                new CodeInstruction(OpCodes.Dup),
                new CodeInstruction(OpCodes.Brtrue, label2),
                new CodeInstruction(OpCodes.Pop),
                new CodeInstruction(OpCodes.Br, label3),
                new CodeInstruction(OpCodes.Dup).WithLabels([label2]),
                new CodeInstruction(OpCodes.Dup),
                new CodeInstruction(OpCodes.Callvirt, getPercent),
                new CodeInstruction(OpCodes.Ldc_R4, 0.02f),
                new CodeInstruction(OpCodes.Add),
                new CodeInstruction(OpCodes.Callvirt, AccessTools.PropertySetter(typeof(Need), nameof(Need.CurLevelPercentage))),
                new CodeInstruction(OpCodes.Callvirt, getPercent),
                CodeInstruction.StoreLocal(16),
                new CodeInstruction(OpCodes.Br, label3)
            ]);
            codeMatcher.MatchStartForward(CodeMatch.LoadsLocal(name: "V_16"));
            codeMatcher.ThrowIfInvalid("Failed to find end block.");
            codeMatcher.AddLabels([label3]);
            return codeMatcher.InstructionEnumeration();
        }
    }
    /*[HarmonyPatch]
    public static class PowerPPMechs_AddMechsToChecks_Patch
    {
        private static readonly MethodInfo getPercent = AccessTools.PropertyGetter(typeof(Need), nameof(Need.CurLevelPercentage));
        public static IEnumerable<MethodBase> TargetMethods()
        {
            return [AccessTools.Method(typeof(GC_PPP), nameof(GC_PPP.GameComponentTick)), GenTypes.GetTypeInAnyAssembly("aRandomKiwi.PPP.Pawn_MindState_Patch").Method("Listener")];
        }

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            CodeMatcher codeMatcher = new(instructions, generator);
            codeMatcher.MatchStartForward(CodeMatch.LoadsLocal(name: "V_8"), CodeMatch.Calls(PowerPPMechs.utils.Method("IsMiscRobot")));
            codeMatcher.ThrowIfInvalid("Failed to find IsMiscRobot call.");
            codeMatcher.CreateLabel(out Label label1);
            codeMatcher.DefineLabel(out Label label2);
            codeMatcher.DefineLabel(out Label label3);
            codeMatcher.Insert([
                CodeInstruction.LoadLocal((int)codeMatcher.Instruction.operand),
                new CodeInstruction(OpCodes.Callvirt, AccessTools.PropertyGetter(typeof(Pawn), nameof(Pawn.IsColonyMech))),
                new CodeInstruction(OpCodes.Brfalse, label1),
                new CodeInstruction(OpCodes.Ldloc_S, codeMatcher.Instruction.operand),
                CodeInstruction.LoadField(typeof(Pawn), nameof(Pawn.needs)),
                CodeInstruction.LoadField(typeof(Pawn_NeedsTracker), nameof(Pawn_NeedsTracker.energy)),
                new CodeInstruction(OpCodes.Dup),
                new CodeInstruction(OpCodes.Brtrue, label2),
                new CodeInstruction(OpCodes.Pop),
                new CodeInstruction(OpCodes.Br, label3),
                new CodeInstruction(OpCodes.Dup).WithLabels([label2]),
                new CodeInstruction(OpCodes.Dup),
                new CodeInstruction(OpCodes.Callvirt, getPercent),
                new CodeInstruction(OpCodes.Ldc_R4, 0.02f),
                new CodeInstruction(OpCodes.Add),
                new CodeInstruction(OpCodes.Callvirt, AccessTools.PropertySetter(typeof(Need), nameof(Need.CurLevelPercentage))),
                new CodeInstruction(OpCodes.Callvirt, getPercent),
                CodeInstruction.StoreLocal(16),
                new CodeInstruction(OpCodes.Br, label3)
            ]);
            codeMatcher.MatchStartForward(CodeMatch.LoadsLocal(name: "V_16"));
            codeMatcher.ThrowIfInvalid("Failed to find end block.");
            codeMatcher.AddLabels([label3]);
            return codeMatcher.InstructionEnumeration();
        }
    }*/
}
