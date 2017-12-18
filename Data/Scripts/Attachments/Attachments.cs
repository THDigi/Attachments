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
using Sandbox.ModAPI.Interfaces.Terminal;
using VRage.Common.Utils;
using VRage.Game;
using VRage.Game.ModAPI;
using VRageMath;
using VRage;
using VRage.ObjectBuilders;
using VRage.Game.Components;
using VRage.ModAPI;
using VRage.Utils;

namespace Digi.Attachments
{
    [MySessionComponentDescriptor(MyUpdateOrder.AfterSimulation)]
    public class AttachmentsMod : MySessionComponentBase
    {
        public override void LoadData()
        {
            instance = this;
            Log.SetUp("Attachments", 516770964);
        }

        public static AttachmentsMod instance = null;

        public bool init = false;
        public bool parsedTerminalControls = false;

        public readonly Dictionary<string, Func<IMyTerminalBlock, bool>> actionEnabledFunc = new Dictionary<string, Func<IMyTerminalBlock, bool>>();
        public readonly Dictionary<string, Func<IMyTerminalBlock, bool>> controlVisibleFunc = new Dictionary<string, Func<IMyTerminalBlock, bool>>();

        public const string ATTACHMENT_BASE = "AttachmentBase";
        public const string ATTACHMENT_BASE_TALL = "AttachmentBaseTall";
        public const string ATTACHMENT_PANEL_TOP = "AttachmentTop";
        public const string ATTACHMENT_TOP_DELETE = "AttachmentTopDelete";
        public const string ATTACHMENT_TOP_TALL_DELETE = "AttachmentTopTallDelete";

        public readonly HashSet<MyStringHash> blockSubtypeIds = new HashSet<MyStringHash>()
        {
            MyStringHash.GetOrCompute(ATTACHMENT_BASE),
            MyStringHash.GetOrCompute(ATTACHMENT_BASE_TALL),
        };

        public void Init()
        {
            init = true;
            Log.Init();
            MyAPIGateway.Utilities.InvokeOnGameThread(() => SetUpdateOrder(MyUpdateOrder.NoUpdate)); // stop updating this component, needed as invoke because it can't be changed mid-update.
        }

        protected override void UnloadData()
        {
            instance = null;
            init = false;
            Log.Close();
        }

        public override void UpdateAfterSimulation()
        {
            try
            {
                if(!init)
                {
                    if(MyAPIGateway.Session == null)
                        return;

                    Init();
                }
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }
    }

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
                isTall = stator.BlockDefinition.SubtypeId == AttachmentsMod.ATTACHMENT_BASE_TALL;

                if(stator.CubeGrid.Physics != null)
                {
                    stator.LowerLimitDeg = 0;
                    stator.UpperLimitDeg = 0;

                    NeedsUpdate = MyEntityUpdateEnum.EACH_FRAME;
                }

                if(!AttachmentsMod.instance.parsedTerminalControls)
                {
                    AttachmentsMod.instance.parsedTerminalControls = true;

                    #region Remove controls on this mod's blocks
                    var controls = new List<IMyTerminalControl>();
                    MyAPIGateway.TerminalControls.GetControls<IMyMotorAdvancedStator>(out controls);

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

                    foreach(var c in controls)
                    {
                        string id = c.Id;

                        if(controlIds.Contains(id))
                        {
                            if(c.Visible != null)
                                AttachmentsMod.instance.controlVisibleFunc[id] = c.Visible; // preserve the existing visible condition

                            c.Visible = (b) =>
                            {
                                var func = AttachmentsMod.instance.controlVisibleFunc.GetValueOrDefault(id, null);
                                return (func == null ? true : func.Invoke(b)) && !AttachmentsMod.instance.blockSubtypeIds.Contains(b.SlimBlock.BlockDefinition.Id.SubtypeId);
                            };
                        }
                    }
                    #endregion

                    #region Remove actions on this mod's blocks
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

                    var actions = new List<IMyTerminalAction>();
                    MyAPIGateway.TerminalControls.GetActions<IMyMotorAdvancedStator>(out actions);

                    foreach(var a in actions)
                    {
                        string id = a.Id;

                        if(actionIds.Contains(id))
                        {
                            if(a.Enabled != null)
                                AttachmentsMod.instance.actionEnabledFunc[id] = a.Enabled;

                            a.Enabled = (b) =>
                            {
                                var func = AttachmentsMod.instance.actionEnabledFunc.GetValueOrDefault(id, null);
                                return (func == null ? true : func.Invoke(b)) && !AttachmentsMod.instance.blockSubtypeIds.Contains(b.SlimBlock.BlockDefinition.Id.SubtypeId);
                            };
                        }
                    }
                    #endregion
                }
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        public override void UpdateAfterSimulation()
        {
            try
            {
                if(stator.PendingAttachment || stator.Top == null || stator.Top.Closed)
                    return;

                var statorMatrix = stator.WorldMatrix;
                var matrix = MatrixD.CreateFromDir(statorMatrix.GetDirectionVector(stator.Top.Orientation.TransformDirectionInverse(Base6Directions.Direction.Forward)),
                                                   statorMatrix.GetDirectionVector(stator.Top.Orientation.TransformDirectionInverse(Base6Directions.Direction.Up)));
                var offset = Vector3D.Transform(stator.Top.Position * stator.TopGrid.GridSize, matrix);
                matrix.Translation = statorMatrix.Translation - offset + (isTall ? statorMatrix.Up : statorMatrix.Down) * (1 + stator.Displacement);
                stator.TopGrid.SetWorldMatrix(matrix);

                if(MyAPIGateway.Multiplayer.IsServer)
                {
                    if(Math.Abs(stator.Angle) > 0)
                    {
                        if(stator.GetValueBool("RotorLock"))
                            stator.SetValueBool("RotorLock", false);

                        stator.TargetVelocityRPM = (stator.Angle > 0 ? 1000 : -1000); // TODO does nothing?!
                    }
                    else
                    {
                        if(!stator.GetValueBool("RotorLock"))
                            stator.SetValueBool("RotorLock", true);
                    }
                }
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }
    }

    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_MotorAdvancedRotor), false, AttachmentsMod.ATTACHMENT_TOP_DELETE, AttachmentsMod.ATTACHMENT_TOP_TALL_DELETE)]
    public class AttachmentTopDelete : MyGameLogicComponent
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
                var gridObj = (MyObjectBuilder_CubeGrid)grid.GetObjectBuilder(false);

                if(gridObj.CubeBlocks.Count > 1) // most likely someone placed this block on a largegrid...
                {
                    grid.RemoveBlock(rotor.SlimBlock);
                    return;
                }

                grid.Close();

                gridObj.GridSizeEnum = MyCubeSize.Small;
                gridObj.CubeBlocks[0].Min = new SerializableVector3I(-2, 0, -2);
                gridObj.CubeBlocks[0].SubtypeName = AttachmentsMod.ATTACHMENT_PANEL_TOP;
                gridObj.PositionAndOrientation = new MyPositionAndOrientation(stator.WorldMatrix.Translation, gridObj.PositionAndOrientation.Value.Forward, gridObj.PositionAndOrientation.Value.Up);

                MyAPIGateway.Entities.RemapObjectBuilder(gridObj);
                var newGrid = (IMyCubeGrid)MyAPIGateway.Entities.CreateFromObjectBuilderAndAdd(gridObj);
                var newRotor = (IMyMotorRotor)newGrid.GetCubeBlock(gridObj.CubeBlocks[0].Min).FatBlock;

                var linearVel = stator.CubeGrid.Physics.LinearVelocity;
                var angularVel = stator.CubeGrid.Physics.AngularVelocity;

                // execute next tick
                MyAPIGateway.Utilities.InvokeOnGameThread(() =>
                {
                    if(newRotor.Closed || stator.Closed)
                        return;

                    stator.Attach(newRotor);

                    // TODO needed?
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