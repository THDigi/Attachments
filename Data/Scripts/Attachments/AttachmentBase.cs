using System;
using System.Collections.Generic;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using VRage.Game.Components;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRageMath;

namespace Digi.Attachments
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_MotorAdvancedStator), false, AttachmentsMod.ATTACHMENT_BASE, AttachmentsMod.ATTACHMENT_BASE_TALL)]
    public class AttachmentBase : MyGameLogicComponent
    {
        private IMyMotorStator stator;
        private bool isTall = false;

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

                isTall = (stator.BlockDefinition.SubtypeId == AttachmentsMod.ATTACHMENT_BASE_TALL);

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

            stator.TopGrid.SetWorldMatrix(matrix);
        }

        private static void EditTerminalControls()
        {
            if(AttachmentsMod.Instance.ParsedTerminalControls)
                return;

            AttachmentsMod.Instance.ParsedTerminalControls = true;
            EditControls();
            EditActions();
        }

        private static void EditControls()
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
                string id = c.Id;

                if(controlIds.Contains(id))
                {
                    // "append" to original Visible function by storing the existing one and calling it along with our condition.

                    if(c.Visible != null)
                        AttachmentsMod.Instance.OriginalControlVisibleFunc[id] = c.Visible;

                    c.Visible = (b) =>
                    {
                        var originalFunc = AttachmentsMod.Instance.OriginalControlVisibleFunc.GetValueOrDefault(id, null);
                        bool originalCondition = (originalFunc == null ? true : originalFunc.Invoke(b));
                        return (originalCondition && !AttachmentsMod.IsAttachmentBaseBlock(b.BlockDefinition));
                    };
                }
            }
        }

        private static void EditActions()
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
                string id = a.Id;

                if(actionIds.Contains(id))
                {
                    // "append" to original Visible function by storing the existing one and calling it along with our condition.

                    if(a.Enabled != null)
                        AttachmentsMod.Instance.OriginalActionEnabledFunc[id] = a.Enabled;

                    a.Enabled = (b) =>
                    {
                        var originalFunc = AttachmentsMod.Instance.OriginalActionEnabledFunc.GetValueOrDefault(id, null);
                        bool originalCondition = (originalFunc == null ? true : originalFunc.Invoke(b));
                        return (originalCondition && !AttachmentsMod.IsAttachmentBaseBlock(b.BlockDefinition));
                    };
                }
            }
        }
    }
}