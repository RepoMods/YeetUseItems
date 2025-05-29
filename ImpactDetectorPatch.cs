using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Photon.Pun;
using UnityEngine;

namespace YeetUseItems;

[HarmonyPatch(typeof(PhysGrabObjectImpactDetector))]
public class ImpactDetectorPatch
{

    public static void PlayerCollided(PhysGrabObjectImpactDetector instance, PlayerAvatar player)
    {
        var upgrade = instance.GetComponentInParent<ItemUpgrade>();
        var healthpack = instance.GetComponentInParent<ItemHealthPack>();
        if(!upgrade && !healthpack) return;
        if (upgrade && healthpack)
        {
            YeetUseItems.Logger.LogWarning("Item is both a upgrade and a health pack?!");
            YeetUseItems.Logger.LogWarning("Item is going to be ignored!");
            return;
        }

        if (upgrade)
        {
            if(!upgrade.isPlayerUpgrade || !upgrade.itemToggle.enabled) return;
            ToggleItem(player, upgrade.itemToggle);
        }
        else if (healthpack)
        { 
            if(SemiFunc.RunIsShop()) return;
            if(!SemiFunc.IsMasterClientOrSingleplayer() || !healthpack.itemToggle.enabled || healthpack.used) return;
            if(player.playerHealth.health >= player.playerHealth.maxHealth) return;
            ToggleItem(player, healthpack.itemToggle);
        }
    }

    private static void ToggleItem(PlayerAvatar player, ItemToggle toggle)
    {
        toggle.ToggleItem(true, player.photonView.ViewID);
    }
    
    [HarmonyTranspiler, HarmonyPatch(nameof(PhysGrabObjectImpactDetector.OnCollisionStay))]
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        YeetUseItems.Logger.LogInfo("Patching PhysGrabObjectImpactDetector::OnCollisionStay ...");
        var codes = new List<CodeInstruction>(instructions);
        var methodToCall = typeof(ImpactDetectorPatch).GetMethod("PlayerCollided", BindingFlags.Public | BindingFlags.Static);
        var targetMethod = typeof(PlayerTumble).GetMethod("TumbleRequest", BindingFlags.Public | BindingFlags.Instance);

        if (methodToCall == null)
        {
            YeetUseItems.Logger.LogError("Hook method is null");
            YeetUseItems.Logger.LogError("Plugin will not work.");
            return codes;
        }
        
        for (var i = 0; i < codes.Count; i++)
        {
            var cur = codes[i];

            if (cur.opcode != OpCodes.Callvirt || cur.operand is not MethodInfo)
                continue;
                    
            var method = cur.operand as MethodInfo;
            if(method != targetMethod) continue;
            var parameters = method.GetParameters();
            
            // We want the PlayerTumble instance
            // self <-- We want this
            // isTumbling
            // playerInput
            // Call TumbleRequest <-- We are there

            var offset = i - parameters.Length - 1;
            
            var self = codes[offset - 3];

            if (self.opcode != OpCodes.Ldloc_S)
            {
                YeetUseItems.Logger.LogError("No PlayerAvatar reference found");
                YeetUseItems.Logger.LogError("Plugin will not work.");
                break;
            }

            if (self.operand is not LocalBuilder fieldInfo || fieldInfo.LocalType != typeof(PlayerAvatar))
            {
                YeetUseItems.Logger.LogError("'this' parameter is not of type PlayerAvatar.");
                YeetUseItems.Logger.LogError("Plugin will not work.");
                break;
            }

            codes.Insert(offset, new CodeInstruction(OpCodes.Call, methodToCall));
            codes.Insert(offset, new CodeInstruction(OpCodes.Ldloc_S, self.operand));
            codes.Insert(offset, new CodeInstruction(OpCodes.Ldarg_0));

            YeetUseItems.Logger.LogInfo("Patched successfully");
            break;
        }
        
        return codes;
    }
}