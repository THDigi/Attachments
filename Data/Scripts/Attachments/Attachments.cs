using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Sandbox.Common;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.Game.Gui;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces;
using VRage.Common.Utils;
using VRage.Game;
using VRage.Game.ModAPI;
using VRageMath;
using VRage;
using VRage.ObjectBuilders;
using VRage.Game.Components;
using VRage.ModAPI;
using VRage.Utils;

using Digi.Utils;

namespace Digi.Attachments
{
    [MySessionComponentDescriptor(MyUpdateOrder.AfterSimulation)]
    public class Attachments : MySessionComponentBase
    {
        public static bool init { get; private set; }
        
        public void Init()
        {
            Log.Init();
            Log.Info("Initialized.");
            init = true;
        }
        
        protected override void UnloadData()
        {
            try
            {
                if(init)
                {
                    init = false;
                    Log.Info("Mod unloaded.");
                }
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
            
            Log.Close();
        }
        
        public override void UpdateAfterSimulation()
        {
            if(!init)
            {
                if(MyAPIGateway.Session == null)
                    return;
                
                Init();
            }
        }
    }
    
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_MotorAdvancedStator), "AttachmentBase", "AttachmentBaseTall")]
    public class AttachmentBase : MyGameLogicComponent
    {
        private bool tall = false;
        private byte skip = 0;
        private byte justAttached = 0;
        
        private static BoundingSphereD sphere = new BoundingSphereD(Vector3D.Zero, 1);
        
        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            Entity.NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME | MyEntityUpdateEnum.EACH_FRAME;
            
            tall = (Entity as IMyCubeBlock).BlockDefinition.SubtypeId == "AttachmentBaseTall";
        }
        
        public override void UpdateAfterSimulation()
        {
            try
            {
                var stator = Entity as IMyMotorStator;
                
                //MyAPIGateway.Utilities.ShowNotification("rotor="+(stator.Rotor != null)+"; "+(stator.IsAttached?"IsAttached; ":"")+(stator.PendingAttachment?"Pending; ":"")+(stator.IsLocked?"IsLocked":""), 16, MyFontEnum.Red);
                
                if(stator.IsWorking && (stator.PendingAttachment || stator.Rotor == null))
                {
                    if(++skip >= 15)
                    {
                        skip = 0;
                        sphere.Center = stator.WorldMatrix.Translation;
                        var ents = MyAPIGateway.Entities.GetEntitiesInSphere(ref sphere);
                        
                        foreach(var ent in ents)
                        {
                            var rotor = ent as IMyMotorRotor;
                            
                            if(rotor != null && rotor.CubeGrid.Physics != null)
                            {
                                stator.Attach(rotor);
                                justAttached = 5; // check if it's locked for the next 5 update frames including this one
                                break;
                            }
                        }
                    }
                }
                
                if(stator.Rotor == null || stator.Rotor.Closed)
                    return;
                
                if(justAttached > 0)
                {
                    justAttached--;
                    
                    if(stator.IsLocked)
                    {
                        justAttached = 0;
                        stator.ApplyAction("Force weld"); // disable safety lock after attaching it because the IsLocked check doesn't work when not attached
                    }
                }
                
                if(stator.IsLocked)
                    return;
                
                var statorMatrix = stator.WorldMatrix;
                var matrix = MatrixD.CreateFromDir(statorMatrix.GetDirectionVector(stator.Rotor.Orientation.TransformDirectionInverse(Base6Directions.Direction.Forward)),
                                                   statorMatrix.GetDirectionVector(stator.Rotor.Orientation.TransformDirectionInverse(Base6Directions.Direction.Up)));
                var offset = Vector3D.Transform(stator.Rotor.Position * stator.RotorGrid.GridSize, matrix);
                matrix.Translation = statorMatrix.Translation - offset + (tall ? statorMatrix.Up : statorMatrix.Down) * (1 + stator.Displacement);
                stator.RotorGrid.SetWorldMatrix(matrix);
                
                stator.ApplyAction("Force weld"); // re-enable safety lock after we know the top part is aligned properly
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }
        
        public override MyObjectBuilder_EntityBase GetObjectBuilder(bool copy = false)
        {
            return Entity.GetObjectBuilder(copy);
        }
    }
    
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_MotorAdvancedRotor), "AttachmentTopDelete", "AttachmentTopTallDelete")]
    public class AttachmentTopDelete : MyGameLogicComponent
    {
        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            Entity.NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
        }
        
        public override void UpdateOnceBeforeFrame()
        {
            try
            {
                if(!MyAPIGateway.Multiplayer.IsServer)
                    return;
                
                MyObjectBuilder_CubeGrid gridObj = null;
                
                var rotor = Entity as IMyMotorRotor;
                var stator = rotor.Stator;
                var subTypeId = rotor.BlockDefinition.SubtypeId;
                var grid = rotor.CubeGrid as IMyCubeGrid;
                gridObj = grid.GetObjectBuilder(false) as MyObjectBuilder_CubeGrid;
                grid.Close();
                
                if(gridObj == null)
                {
                    Log.Error("Unable to get the rotor head grid's object builder!");
                    return;
                }
                
                gridObj.GridSizeEnum = MyCubeSize.Small;
                gridObj.CubeBlocks[0].Min = new SerializableVector3I(-2, 0, -2);
                gridObj.CubeBlocks[0].SubtypeName = "AttachmentTop";
                
                //gridObj.PositionAndOrientation = new MyPositionAndOrientation(stator.WorldMatrix.Translation, gridObj.PositionAndOrientation.Value.Forward, gridObj.PositionAndOrientation.Value.Up);
                MyAPIGateway.Entities.RemapObjectBuilder(gridObj);
                var newRotor = MyAPIGateway.Entities.CreateFromObjectBuilderAndAdd(gridObj) as IMyMotorRotor;
                
                stator.Attach(newRotor);
                
                // most likely not required as grids seem to be sent automatically when spawned from server
                //MyAPIGateway.Multiplayer.SendEntitiesCreated(new List<MyObjectBuilder_EntityBase>(1) { gridObj });
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }
        
        public override MyObjectBuilder_EntityBase GetObjectBuilder(bool copy = false)
        {
            return Entity.GetObjectBuilder(copy);
        }
    }
}