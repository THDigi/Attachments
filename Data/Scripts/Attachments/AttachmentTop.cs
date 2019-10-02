using System;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRageMath;

namespace Digi.Attachments
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_MotorAdvancedRotor), false,
        AttachmentsMod.ATTACHMENT_TOP_SMALL,
        AttachmentsMod.ATTACHMENT_TOP_LARGE)]
    public class AttachmentTop : MyGameLogicComponent
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
                var rotorGrid = rotor.CubeGrid;

                if(rotorGrid == null || rotorGrid.Physics == null || rotorGrid.MarkedForClose)
                    return; // ignore ghost grids and whatnot

                var internalGrid = (MyCubeGrid)rotorGrid;
                if(internalGrid.BlocksCount > 1)
                    return; // it's already part of a larger grid, ignore

                var stator = rotor.Base;

                if(stator == null)
                    return; // block is not attached, ignore.

                if(stator.CubeGrid?.Physics == null || stator.CubeGrid.MarkedForClose)
                    return; // ignore ghost grids and whatnot

                if(stator.CubeGrid.GridSizeEnum != rotor.CubeGrid.GridSizeEnum)
                    return; // already small to large or vice versa

                if(!AttachmentsMod.IsAttachmentBaseBlock(stator.BlockDefinition))
                    return; // attached to unknown rotor base, ignore.

                ReplaceTop(rotor);
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        void ReplaceTop(IMyMotorRotor rotor)
        {
            var data = new AttachData();

            var stator = rotor.Base;
            data.Stator = stator;
            var gridObj = (MyObjectBuilder_CubeGrid)rotor.CubeGrid.GetObjectBuilder();

            rotor.CubeGrid.Close();

            if(gridObj.GridSizeEnum == MyCubeSize.Large)
            {
                data.BlockPos = new SerializableVector3I(-2, 0, -2);
                gridObj.GridSizeEnum = MyCubeSize.Small;
                gridObj.CubeBlocks[0].SubtypeName = AttachmentsMod.ATTACHMENT_TOP_SMALL;
            }
            else
            {
                data.BlockPos = new SerializableVector3I(0, 0, 0);
                gridObj.GridSizeEnum = MyCubeSize.Large;
                gridObj.CubeBlocks[0].SubtypeName = AttachmentsMod.ATTACHMENT_TOP_LARGE;
            }

            gridObj.CubeBlocks[0].Min = data.BlockPos;

            gridObj.PositionAndOrientation = new MyPositionAndOrientation(stator.WorldMatrix.Translation, gridObj.PositionAndOrientation.Value.Forward, gridObj.PositionAndOrientation.Value.Up);

            MyAPIGateway.Entities.RemapObjectBuilder(gridObj);

            data.NewGrid = (IMyCubeGrid)MyAPIGateway.Entities.CreateFromObjectBuilderAndAdd(gridObj);

            data.LinearVel = stator.CubeGrid.Physics.LinearVelocity;
            data.AngularVel = stator.CubeGrid.Physics.AngularVelocity;

            MyAPIGateway.Utilities.InvokeOnGameThread(data.Action);
        }

        class AttachData
        {
            public IMyMotorBase Stator;
            public IMyCubeGrid NewGrid;
            public Vector3I BlockPos;
            public Vector3 LinearVel;
            public Vector3 AngularVel;

            public void Action()
            {
                try
                {
                    if(Stator == null || Stator.MarkedForClose)
                        return;

                    var newRotor = NewGrid.GetCubeBlock(BlockPos)?.FatBlock as IMyMotorRotor;

                    if(newRotor == null || newRotor.MarkedForClose)
                        return;

                    Stator.Attach(newRotor);
                    Stator.CubeGrid.Physics.LinearVelocity = LinearVel;
                    Stator.CubeGrid.Physics.AngularVelocity = AngularVel;
                }
                catch(Exception e)
                {
                    Log.Error(e);
                }
            }
        }
    }
}