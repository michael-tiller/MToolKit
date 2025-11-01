using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using MToolKit.Runtime.Persistence.Enums;
using MToolKit.Runtime.Persistence.Interfaces;
using MToolKit.Runtime.Persistence.ES3Integration;
using MToolKit.Template.ExamplePlayer.Interface;
using Serilog;
using UnityEngine;
using UnityEngine.SceneManagement;
using ILogger = Serilog.ILogger;


/// <summary>
/// Namespace for player persistence.
/// </summary>
namespace MToolKit.Template.ExamplePlayer.Persistence
{
    /// <summary>
    /// Save domain controller for player data (position and orientation)
    /// </summary>
    public class PlayerSaveController : ISaveDomainController
    {
        private static readonly Lazy<ILogger> logLazy = new(() => Log.Logger.ForContext<PlayerSaveController>().ForFeature("Template.Player"));
        private static ILogger log => logLazy.Value ?? Serilog.Core.Logger.None;
        
        private readonly IExamplePlayerService playerService;
        private readonly IES3Service es3Service;
        
        public ESaveDomain Domain => ESaveDomain.Player;
        
        public PlayerSaveController(IExamplePlayerService playerService, IES3Service es3Service)
        {
            this.playerService = playerService ?? throw new System.ArgumentNullException(nameof(playerService));
            this.es3Service = es3Service ?? throw new System.ArgumentNullException(nameof(es3Service));
            log.ForMethod().Debug("[PLAYER_LOAD] PlayerSaveController created for domain: {Domain} - Instance: {InstanceId}", Domain, GetHashCode());
        }
        
        public async UniTask SaveAsync(CancellationToken ct = default)
        {
            log.ForMethod().Information("Saving player data for domain: {Domain}", Domain);
            
            try
            {
                var transform = playerService.PlayerTransform;
                if (transform == null)
                {
                    log.ForMethod().Warning("Player transform is null, skipping save");
                    return;
                }
                
                // Create player data from current transform
                var currentSceneNumber = SceneManager.GetActiveScene().buildIndex;
                var playerData = ExamplePlayerData.FromTransform(transform, currentSceneNumber);
                
                // Save the player data using the profile-aware ES3 service
                await es3Service.SaveAsync("PlayerData", playerData, ct);
                
                // Also save additional player state if needed
                SaveAdditionalPlayerData(transform);
                
                log.ForMethod().Information("Successfully saved player data: position={Position}, rotation={Rotation}, scene={SceneNumber}", 
                    playerData.Position, playerData.Rotation.eulerAngles, playerData.SceneNumber);
            }
            catch (System.Exception ex)
            {
                log.ForMethod().Error(ex, "Failed to save player data for domain: {Domain}", Domain);
                throw;
            }
        }
        
        /// <summary>
        /// Override this method to save additional player-specific data
        /// </summary>
        protected virtual void SaveAdditionalPlayerData(Transform playerTransform)
        {
            // Default implementation saves no additional data
            // Subclasses can override to save health, inventory, etc.
        }
        
        private bool hasLoadedData = false;
        
        public async UniTask LoadAsync(CancellationToken ct = default)
        {
            log.ForMethod().Verbose("[PLAYER_LOAD] Loading player data for domain: {Domain}", Domain);
            
            try
            {
                var transform = playerService.PlayerTransform;
                if (transform == null)
                {
                    log.ForMethod().Warning("[PLAYER_LOAD] Player transform is null, player may not be spawned yet. Skipping load - this is expected during initial scene load.");
                    return;
                }
                
                // Prevent loading the same data multiple times
                if (hasLoadedData)
                {
                    log.ForMethod().Debug("[PLAYER_LOAD] Player data already loaded, skipping duplicate load");
                    return;
                }
                
                log.ForMethod().Debug("[PLAYER_LOAD] Player transform found, current position: {Position}, rotation: {Rotation}", 
                    transform.position, transform.rotation.eulerAngles);
                
                // Check if save data exists before loading
                bool hasSaveData = es3Service.KeyExists("PlayerData");
                log.ForMethod().Debug("[PLAYER_LOAD] Save data exists: {HasSaveData}", hasSaveData);
                
                // Load the player data using the profile-aware ES3 service
                var playerData = await es3Service.LoadAsync("PlayerData", new ExamplePlayerData(), ct);
                
                log.ForMethod().Debug("[PLAYER_LOAD] Loaded player data: position={Position}, rotation={Rotation}, scene={SceneNumber}", 
                    playerData.Position, playerData.Rotation.eulerAngles, playerData.SceneNumber);
                
                // Store the current position before applying loaded data
                var currentPosition = transform.position;
                var currentRotation = transform.rotation;
                
                // Only apply the loaded data if it's not the default (zero position) and represents a meaningful change
                // This prevents overwriting a valid spawn position with default values
                bool shouldApplyData = false;
                
                if (playerData.Position != Vector3.zero || playerData.Rotation != Quaternion.identity)
                {
                    // Additional validation: only apply if the loaded position is significantly different from current
                    // This prevents unnecessary position changes when the player is already in the correct position
                    float positionDifference = Vector3.Distance(currentPosition, playerData.Position);
                    float rotationDifference = Quaternion.Angle(currentRotation, playerData.Rotation);
                    
                    // Apply if there's a meaningful difference (more than 0.1 units or 1 degree)
                    if (positionDifference > 0.1f || rotationDifference > 1.0f)
                    {
                        shouldApplyData = true;
                        log.ForMethod().Debug("[PLAYER_LOAD] Position difference: {PositionDiff:F3}, rotation difference: {RotationDiff:F1} degrees - will apply", 
                            positionDifference, rotationDifference);
                    }
                    else
                    {
                        log.ForMethod().Debug("[PLAYER_LOAD] Position difference: {PositionDiff:F3}, rotation difference: {RotationDiff:F1} degrees - too small, keeping current position", 
                            positionDifference, rotationDifference);
                    }
                }
                else
                {
                    log.ForMethod().Information("[PLAYER_LOAD] Loaded default player data (zero position), keeping current spawn position: {Position}", 
                        transform.position);
                }
                
                if (shouldApplyData)
                {
                    log.ForMethod().Verbose("[PLAYER_LOAD] Before applying data - transform position: {Position}, loaded position: {LoadedPosition}", 
                        transform.position, playerData.Position);
                    
                    // Apply the loaded data to the transform
                    playerData.ApplyToTransform(transform);
                    
                    log.ForMethod().Verbose("[PLAYER_LOAD] After applying data - transform position: {Position}", transform.position);
                    
                    log.ForMethod().Debug("[PLAYER_LOAD] Successfully loaded player data: position={Position}, rotation={Rotation}, scene={SceneNumber}", 
                        playerData.Position, playerData.Rotation.eulerAngles, playerData.SceneNumber);
                }
                else
                {
                    log.ForMethod().Debug("[PLAYER_LOAD] Keeping current player position: {Position}, rotation: {Rotation}", 
                        transform.position, transform.rotation.eulerAngles);
                }
                
                hasLoadedData = true;
                
                // Load additional player state if available
                LoadAdditionalPlayerData(transform);
            }
            catch (System.Exception ex)
            {
                log.ForMethod().Error(ex, "Failed to load player data for domain: {Domain}", Domain);
                throw;
            }
        }
        
        /// <summary>
        /// Override this method to load additional player-specific data
        /// </summary>
        protected virtual void LoadAdditionalPlayerData(Transform playerTransform)
        {
            // Default implementation loads no additional data
            // Subclasses can override to load health, inventory, etc.
        }
        
        /// <summary>
        /// Check if player save data exists
        /// </summary>
        public bool HasSaveData()
        {
            return es3Service.KeyExists("PlayerData");
        }
        
        /// <summary>
        /// Clear all player save data
        /// </summary>
        public void ClearSaveData()
        {
            es3Service.DeleteKey("PlayerData");
            log.ForMethod().Information("Cleared all player save data");
        }
        
        /// <summary>
        /// Reset the load state - useful when the controller is recreated
        /// </summary>
        public void ResetLoadState()
        {
            hasLoadedData = false;
            log.ForMethod().Debug("[PLAYER_LOAD] PlayerSaveController load state reset - Instance: {InstanceId}", GetHashCode());
        }
        
        /// <summary>
        /// Force reload player position from save data - useful after scene loads
        /// </summary>
        public async UniTask ReloadPlayerPositionAsync(CancellationToken ct = default)
        {
            log.ForMethod().Verbose("[PLAYER_LOAD] Force reloading player position after scene load");

            await UniTask.Delay(100, cancellationToken: ct);
            
            var transform = playerService.PlayerTransform;
            if (transform == null)
            {
                log.ForMethod().Warning("[PLAYER_LOAD] Player transform is null during reload, player may not be spawned yet");
                return;
            }
            
            // Store current position for validation
            var currentPosition = transform.position;
            var currentRotation = transform.rotation;
            
            log.ForMethod().Verbose("[PLAYER_LOAD] Current player position before reload: {Position}, rotation: {Rotation}", 
                currentPosition, currentRotation.eulerAngles);
            
            // Reset the load state to allow reloading
            hasLoadedData = false;
            
            // Load the player data
            await LoadAsync(ct);
            
            // Validate that the position was actually changed and is valid
            var newPosition = transform.position;
            var newRotation = transform.rotation;
            
            log.ForMethod().Verbose("[PLAYER_LOAD] Player position after reload: {Position}, rotation: {Rotation}", 
                newPosition, newRotation.eulerAngles);
            
            // Check if the position was actually updated (not just default values)
            if (Vector3.Distance(currentPosition, newPosition) < 0.01f && 
                Quaternion.Angle(currentRotation, newRotation) < 0.1f)
            {
                log.ForMethod().Debug("[PLAYER_LOAD] Player position unchanged after reload - this may indicate no valid save data or position was already correct");
            }
            else
            {
                log.ForMethod().Debug("[PLAYER_LOAD] Player position successfully updated from save data");
            }
        }
    }
}
