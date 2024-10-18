using Oxide.Core.Plugins;
using UnityEngine;
using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("ChairTrap", "Cornholio21", "1.0.10")]
    [Description("Spawns a chair and forces a selected player to sit on it until released.")]
    public class ChairTrap : RustPlugin
    {
        private const string ChairPrefab = "assets/prefabs/deployable/chair/chair.deployed.prefab";
        private const string PermissionUse = "chairtrap.use";
        private Dictionary<ulong, BaseEntity> playerChairs = new Dictionary<ulong, BaseEntity>();
        private Dictionary<ulong, BasePlayer> trappedPlayers = new Dictionary<ulong, BasePlayer>();

        private void Init()
        {
            cmd.AddChatCommand("chair", this, "CmdTrapChair");
            cmd.AddChatCommand("unchair", this, "CmdReleaseChair");
            permission.RegisterPermission(PermissionUse, this);
        }

        private void CmdTrapChair(BasePlayer adminPlayer, string command, string[] args)
        {
            if (!permission.UserHasPermission(adminPlayer.UserIDString, PermissionUse))
            {
                adminPlayer.ChatMessage("You do not have permission to use this command.");
                return;
            }

            if (args.Length == 0)
            {
                adminPlayer.ChatMessage("Usage: /chair <playername>");
                return;
            }

            var targetPlayer = FindPlayer(args[0]);
            if (targetPlayer == null)
            {
                adminPlayer.ChatMessage($"Player '{args[0]}' not found.");
                return;
            }

            Vector3 chairPosition;
            if (TryGetLookPoint(adminPlayer, out chairPosition))
            {
                BaseEntity chairEntity = GameManager.server.CreateEntity(ChairPrefab, chairPosition);
                if (chairEntity != null)
                {
                    chairEntity.transform.rotation = Quaternion.LookRotation(adminPlayer.transform.position - chairPosition);
                    chairEntity.Spawn();
                    playerChairs[targetPlayer.userID] = chairEntity;
                    SitPlayerOnChair(targetPlayer, chairEntity);
                    trappedPlayers[targetPlayer.userID] = targetPlayer;
                    adminPlayer.ChatMessage($"Player {targetPlayer.displayName} has been seated on a chair facing you.");
                }
                else
                {
                    adminPlayer.ChatMessage("Failed to spawn the chair.");
                }
            }
            else
            {
                adminPlayer.ChatMessage("Could not find a valid location in front of you to place the chair.");
            }
        }
        private void CmdReleaseChair(BasePlayer adminPlayer, string command, string[] args)
        {
            if (!permission.UserHasPermission(adminPlayer.UserIDString, PermissionUse))
            {
                adminPlayer.ChatMessage("You do not have permission to use this command.");
                return;
            }

            if (args.Length == 0)
            {
                adminPlayer.ChatMessage("Usage: /unchair <playername>");
                return;
            }
            var targetPlayer = FindPlayer(args[0]);
            if (targetPlayer == null)
            {
                adminPlayer.ChatMessage($"Player '{args[0]}' not found.");
                return;
            }
            if (trappedPlayers.ContainsKey(targetPlayer.userID))
            {
                ReleasePlayerFromChair(targetPlayer);
                DestroyChair(targetPlayer.userID);
                trappedPlayers.Remove(targetPlayer.userID);
                adminPlayer.ChatMessage($"Player {targetPlayer.displayName} has been released from the chair.");
            }
            else
            {
                adminPlayer.ChatMessage("That player is not trapped on a chair.");
            }
        }
        private BasePlayer FindPlayer(string nameOrId)
        {
            BasePlayer player = BasePlayer.Find(nameOrId);
            if (player == null)
            {
                foreach (var p in BasePlayer.activePlayerList)
                {
                    if (p.displayName.ToLower().Contains(nameOrId.ToLower()))
                    {
                        return p;
                    }
                }
            }
            return player;
        }
        private void SitPlayerOnChair(BasePlayer player, BaseEntity chairEntity)
        {
            BaseMountable mountable = chairEntity as BaseMountable;
            if (mountable != null)
            {
                mountable.MountPlayer(player);
            }
        }
        private void ReleasePlayerFromChair(BasePlayer player)
        {
            if (player.GetMounted() != null)
            {
                player.GetMounted().DismountPlayer(player);
            }
        }
        private void DestroyChair(ulong userID)
        {
            if (playerChairs.ContainsKey(userID))
            {
                playerChairs[userID]?.Kill();
                playerChairs.Remove(userID);
            }
        }
        private void OnPlayerDeath(BasePlayer player, HitInfo info)
        {
            if (trappedPlayers.ContainsKey(player.userID))
            {
                player.ChatMessage("You are trapped and will be reseated after respawning.");
            }
        }
        private void OnPlayerRespawned(BasePlayer player)
        {
            if (trappedPlayers.ContainsKey(player.userID))
            {
                timer.Once(1f, () =>
                {
                    if (playerChairs.ContainsKey(player.userID))
                    {
                        BaseEntity chairEntity = playerChairs[player.userID];
                        player.Teleport(chairEntity.transform.position + Vector3.up * 0.5f);
                        SitPlayerOnChair(player, chairEntity);
                    }
                });
            }
        }
        private object CanDismountEntity(BasePlayer player, BaseMountable entity)
        {
            if (trappedPlayers.ContainsKey(player.userID))
            {
                player.ChatMessage("You are trapped and cannot stand up!");
                return false;
            }
            return null;
        }
        private bool TryGetLookPoint(BasePlayer player, out Vector3 hitPosition)
        {
            RaycastHit hit;
            Vector3 eyePosition = player.eyes.position;
            Vector3 forwardDirection = player.eyes.BodyForward();
            if (Physics.Raycast(eyePosition, forwardDirection, out hit, 100f))
            {
                hitPosition = hit.point;
                return true;
            }
            else
            {
                hitPosition = Vector3.zero;
                return false;
            }
        }
    }
}