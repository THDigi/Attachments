﻿using System;
using System.Collections.Generic;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRageMath;

namespace Digi.Attachments
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_MotorAdvancedStator), false,
        AttachmentsMod.ATTACHMENT_BASE_SMALL,
        AttachmentsMod.ATTACHMENT_BASE_LARGE,
        AttachmentsMod.ATTACHMENT_BASE_LARGE_TALL)]
    public class AttachmentBase : MyGameLogicComponent
    {
        IMyMotorStator stator;
        bool isTall = false;
        MyCubeGrid LinkedTo;

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            stator = (IMyMotorStator)Entity;
            NeedsUpdate = MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
        }

        public override void UpdateOnceBeforeFrame()
        {
            try
            {
                if(AttachmentsMod.Instance == null)
                {
                    NeedsUpdate = MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
                    return;
                }

                EditTerminalControls();

                isTall = (stator.BlockDefinition.SubtypeId == AttachmentsMod.ATTACHMENT_BASE_LARGE_TALL);

                if(stator.CubeGrid.Physics == null)
                    return;

                if(MyAPIGateway.Multiplayer.IsServer)
                {
                    var def = ((MyMotorStatorDefinition)stator.SlimBlock.BlockDefinition);

                    stator.LowerLimitDeg = 0;
                    stator.UpperLimitDeg = 0;
                    stator.Torque = def.UnsafeTorqueThreshold;
                    stator.BrakingTorque = def.UnsafeTorqueThreshold;
                }

                NeedsUpdate = MyEntityUpdateEnum.EACH_FRAME;

                stator.OnAttachedChanged += Stator_OnAttachedChanged;
                Stator_OnAttachedChanged(stator);
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        void Stator_OnAttachedChanged(IMyMechanicalConnectionBlock _)
        {
            try
            {
                MyCubeGrid thisGrid = (MyCubeGrid)stator.CubeGrid;
                MyCubeGrid attachedGrid = stator.TopGrid as MyCubeGrid;
                long linkId = stator.EntityId;

                if(attachedGrid != null) // attached
                {
                    if(LinkedTo != null)
                    {
                        MyCubeGridGroups.Static.BreakLink(GridLinkTypeEnum.NoContactDamage, linkId, thisGrid, LinkedTo);
                        LinkedTo = null;
                    }

                    if(LinkedTo == null)
                    {
                        MyCubeGridGroups.Static.CreateLink(GridLinkTypeEnum.NoContactDamage, linkId, thisGrid, attachedGrid);
                        LinkedTo = attachedGrid;
                    }
                }
                else // detached
                {
                    if(LinkedTo != null)
                    {
                        MyCubeGridGroups.Static.BreakLink(GridLinkTypeEnum.NoContactDamage, linkId, thisGrid, LinkedTo);
                        LinkedTo = null;
                    }
                }
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        public override void UpdateBeforeSimulation()
        {
            try
            {
                if(stator.PendingAttachment || stator.Top == null || stator.Top.MarkedForClose)
                    return;

                ForceAlignment();
                //HandleRotorLock(); // disabled because it causes shaking when pistons with drills are on it (for example).
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        //private void HandleRotorLock()
        //{
        //    if(!MyAPIGateway.Multiplayer.IsServer)
        //        return; // RotorLock setter is synchronized, only do it serverside
        //
        //    if(Math.Abs(stator.Angle) > 0)
        //    {
        //        if(stator.RotorLock)
        //        {
        //            stator.RotorLock = false;
        //        }
        //    }
        //    else
        //    {
        //        if(!stator.RotorLock)
        //        {
        //            stator.RotorLock = true;
        //        }
        //    }
        //}

        private void ForceAlignment()
        {
            var statorMatrix = stator.WorldMatrix;

            var matrix = MatrixD.CreateFromDir(statorMatrix.GetDirectionVector(stator.Top.Orientation.TransformDirectionInverse(Base6Directions.Direction.Forward)),
                                               statorMatrix.GetDirectionVector(stator.Top.Orientation.TransformDirectionInverse(Base6Directions.Direction.Up)));

            var offset = Vector3D.Transform(stator.Top.Position * stator.TopGrid.GridSize, matrix);
            matrix.Translation = statorMatrix.Translation - offset + (isTall ? statorMatrix.Up : statorMatrix.Down) * (1 + stator.Displacement);

            if(stator.TopGrid.WorldMatrix.EqualsFast(ref matrix, 0.01))
                return; // already close enough, skip!

            stator.TopGrid.SetWorldMatrix(matrix);
        }

        private static void EditTerminalControls()
        {
            if(AttachmentsMod.Instance.ParsedTerminalControls)
                return;

            AttachmentsMod.Instance.ParsedTerminalControls = true;

            var customFunc = new Func<IMyTerminalBlock, bool>(Visible);

            EditControls(customFunc);
            EditActions(customFunc);
        }

        private static bool Visible(IMyTerminalBlock block)
        {
            return !AttachmentsMod.IsAttachmentBaseBlock(block.BlockDefinition);
        }

        private static void EditControls(Func<IMyTerminalBlock, bool> customFunc)
        {
            var controlIds = new HashSet<string>()
            {
                "Add Small Top Part",
                "Reverse",
                "Torque",
                "BrakingTorque",
                "Velocity",
                "LowerLimit",
                "UpperLimit",
                "Displacement",
                "RotorLock",

                // no longer exist...
                "Weld speed",
                "Force weld",
            };

            List<IMyTerminalControl> controls;
            MyAPIGateway.TerminalControls.GetControls<IMyMotorAdvancedStator>(out controls);

            foreach(var c in controls)
            {
                if(controlIds.Contains(c.Id))
                {
                    // append a custom condition after the original condition
                    c.Visible = CombineFunc.Create(c.Visible, customFunc);
                }
            }
        }

        private static void EditActions(Func<IMyTerminalBlock, bool> customFunc)
        {
            var actionIds = new HashSet<string>()
            {
                "Add Small Top Part",
                "Reverse",
                "RotorLock",
                "IncreaseTorque",
                "DecreaseTorque",
                "ResetTorque",
                "IncreaseBrakingTorque",
                "DecreaseBrakingTorque",
                "ResetBrakingTorque",
                "IncreaseVelocity",
                "DecreaseVelocity",
                "ResetVelocity",
                "IncreaseLowerLimit",
                "DecreaseLowerLimit",
                "ResetLowerLimit",
                "IncreaseUpperLimit",
                "DecreaseUpperLimit",
                "ResetUpperLimit",
                "IncreaseDisplacement",
                "DecreaseDisplacement",
                "ResetDisplacement",
            };

            List<IMyTerminalAction> actions;
            MyAPIGateway.TerminalControls.GetActions<IMyMotorAdvancedStator>(out actions);

            foreach(var a in actions)
            {
                if(actionIds.Contains(a.Id))
                {
                    // append a custom condition after the original condition
                    a.Enabled = CombineFunc.Create(a.Enabled, customFunc);
                }
            }
        }
    }
}