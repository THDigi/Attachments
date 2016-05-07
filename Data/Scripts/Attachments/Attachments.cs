using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Timers;
using Sandbox.Common;
using Sandbox.Common.ModAPI;
using Sandbox.Common.Components;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using Sandbox.Engine;
using Sandbox.Engine.Physics;
using Sandbox.Engine.Multiplayer;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.Game.Gui;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces;
using VRage.Common.Utils;
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
            Log.Info("Initialized.");
            init = true;
        }
        
        protected override void UnloadData()
        {
            Log.Info("Mod unloaded.");
            Log.Close();
            init = false;
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
    
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_MotorAdvancedStator), "AttachmentBase")]
    public class AttachmentBase : MyGameLogicComponent
    {
        private MyObjectBuilder_EntityBase objectBuilder;
        private IMyEntity topEnt;
        private int skip = 99;
        
        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            this.objectBuilder = objectBuilder;
            
            Entity.NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME | MyEntityUpdateEnum.EACH_FRAME;
        }
        
        public override void UpdateOnceBeforeFrame()
        {
            var block = Entity as IMyTerminalBlock;
        }
        
        public override void UpdateAfterSimulation()
        {
            try
            {
                if(topEnt == null)
                {
                    if(++skip >= 10)
                    {
                        skip = 0;
                        var obj = (Entity as IMyCubeBlock).GetObjectBuilderCubeBlock(false) as MyObjectBuilder_MotorBase;
                        
                        if(obj.RotorEntityId.HasValue && obj.RotorEntityId.Value != 0)
                        {
                            IMyEntity headEnt;
                            
                            if(!MyAPIGateway.Entities.TryGetEntityById(obj.RotorEntityId.Value, out headEnt) || headEnt.Closed || headEnt.MarkedForClose)
                                return;
                            
                            var headBlock = headEnt as IMyCubeBlock;
                            
                            if(headBlock.BlockDefinition.SubtypeId != "AttachmentTop")
                                return;
                            
                            topEnt = headEnt;
                        }
                        else
                        {
                            (Entity as IMyMotorStator).ApplyAction("Attach");
                        }
                    }
                    
                    return;
                }
                
                if(topEnt.Closed || topEnt.MarkedForClose)
                {
                    topEnt = null;
                    return;
                }
                
                var baseBlock = Entity as IMyMotorStator;
                
                if(!baseBlock.IsAttached)
                {
                    topEnt = null;
                    return;
                }
                
                var topBlock = topEnt as IMyCubeBlock;
                var topGrid = topBlock.CubeGrid as IMyCubeGrid;
                var matrix = MatrixD.CreateFromDir(baseBlock.WorldMatrix.GetDirectionVector(topBlock.Orientation.TransformDirectionInverse(Base6Directions.Direction.Forward)),
                                                   baseBlock.WorldMatrix.GetDirectionVector(topBlock.Orientation.TransformDirectionInverse(Base6Directions.Direction.Up)));
                
                var offset = Vector3D.Transform(topBlock.Position * topGrid.GridSize, matrix);
                
                matrix.Translation = baseBlock.WorldMatrix.Translation - offset + baseBlock.WorldMatrix.Down * (1 + baseBlock.Displacement);
                
                topGrid.SetWorldMatrix(matrix);
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }
        
        public override void Close()
        {
            objectBuilder = null;
            
            var block = Entity as IMyTerminalBlock;
        }
        
        public override MyObjectBuilder_EntityBase GetObjectBuilder(bool copy = false)
        {
            return copy ? (MyObjectBuilder_EntityBase)objectBuilder.Clone() : objectBuilder;
        }
    }
    
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_MotorAdvancedRotor), "AttachmentTopDelete", "AttachmentTopTallDelete")]
    public class AttachmentTopDelete : MyGameLogicComponent
    {
        private MyObjectBuilder_EntityBase objectBuilder;
        
        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            this.objectBuilder = objectBuilder;
            
            Entity.NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
        }
        
        public override void UpdateOnceBeforeFrame()
        {
            try
            {
                if(!MyAPIGateway.Multiplayer.IsServer)
                    return;
                
                MyObjectBuilder_CubeGrid gridObj = null;
                Vector3D center;
                string subTypeId;
                
                {
                    var block = Entity as IMyCubeBlock;
                    subTypeId = block.BlockDefinition.SubtypeId;
                    var grid = block.CubeGrid as IMyCubeGrid;
                    center = block.WorldMatrix.Translation + block.WorldMatrix.Up * 1.2;
                    gridObj = grid.GetObjectBuilder(false) as MyObjectBuilder_CubeGrid;
                    grid.SyncObject.SendCloseRequest();
                }
                
                if(gridObj == null)
                {
                    Log.Error("Unable to get the rotor head grid's object builder!");
                    return;
                }
                
                gridObj.GridSizeEnum = MyCubeSize.Small;
                gridObj.CubeBlocks[0].Min = new SerializableVector3I(-2, 0, -2);
                
                if(subTypeId == "AttachmentTopTallDelete")
                    gridObj.CubeBlocks[0].SubtypeName = "AttachmentTopTall";
                else
                    gridObj.CubeBlocks[0].SubtypeName = "AttachmentTop";
                
                MyAPIGateway.Entities.RemapObjectBuilder(gridObj);
                var ent = MyAPIGateway.Entities.CreateFromObjectBuilderAndAdd(gridObj);
                ent.SetPosition(center);
                
                MyAPIGateway.Multiplayer.SendEntitiesCreated(new List<MyObjectBuilder_EntityBase>(1) { gridObj });
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }
        
        public override void Close()
        {
            objectBuilder = null;
        }
        
        public override MyObjectBuilder_EntityBase GetObjectBuilder(bool copy = false)
        {
            return copy ? (MyObjectBuilder_EntityBase)objectBuilder.Clone() : objectBuilder;
        }
    }
}