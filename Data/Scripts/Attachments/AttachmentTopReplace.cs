using System;
using Sandbox.Common.ObjectBuilders;
using Sandbox.ModAPI;
using VRage;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.ObjectBuilders;

namespace Digi.Attachments
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_MotorAdvancedRotor), false, AttachmentsMod.ATTACHMENT_TOP_DELETE)]
    public class AttachmentTopReplace : MyGameLogicComponent
    {
        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            NeedsUpdate = MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
        }

        public override void UpdateOnceBeforeFrame()
        {
            try
            {
                if(!MyAPIGateway.Multiplayer.IsServer)
                    return;

                var rotor = (IMyMotorRotor)Entity;
                var stator = rotor.Base;
                var grid = rotor.CubeGrid;

                if(grid == null || grid.Physics == null || grid.MarkedForClose)
                    return;

                var gridObj = (MyObjectBuilder_CubeGrid)grid.GetObjectBuilder(false);

                if(gridObj.CubeBlocks.Count > 1) // most likely someone placed this block manually...
                {
                    grid.RemoveBlock(rotor.SlimBlock);
                    return;
                }

                grid.Close();

                gridObj.GridSizeEnum = MyCubeSize.Small;
                gridObj.CubeBlocks[0].Min = new SerializableVector3I(-2, 0, -2);
                gridObj.CubeBlocks[0].SubtypeName = AttachmentsMod.ATTACHMENT_TOP;
                gridObj.PositionAndOrientation = new MyPositionAndOrientation(stator.WorldMatrix.Translation, gridObj.PositionAndOrientation.Value.Forward, gridObj.PositionAndOrientation.Value.Up);

                MyAPIGateway.Entities.RemapObjectBuilder(gridObj);
                var newGrid = (IMyCubeGrid)MyAPIGateway.Entities.CreateFromObjectBuilderAndAdd(gridObj);
                var newRotor = (IMyMotorRotor)newGrid.GetCubeBlock(gridObj.CubeBlocks[0].Min).FatBlock;

                var linearVel = stator.CubeGrid.Physics.LinearVelocity;
                var angularVel = stator.CubeGrid.Physics.AngularVelocity;

                // execute next tick
                MyAPIGateway.Utilities.InvokeOnGameThread(() =>
                {
                    if(newRotor.MarkedForClose || stator.MarkedForClose)
                        return;

                    stator.Attach(newRotor);
                    stator.CubeGrid.Physics.LinearVelocity = linearVel;
                    stator.CubeGrid.Physics.AngularVelocity = angularVel;
                });
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }
    }
}
