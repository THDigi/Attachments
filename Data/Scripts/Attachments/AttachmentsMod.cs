using System;
using System.Collections.Generic;
using Sandbox.Common.ObjectBuilders;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Components;

namespace Digi.Attachments
{
    [MySessionComponentDescriptor(MyUpdateOrder.NoUpdate)]
    public class AttachmentsMod : MySessionComponentBase
    {
        public static AttachmentsMod Instance = null;

        public bool ParsedTerminalControls = false;

        public readonly Dictionary<string, Func<IMyTerminalBlock, bool>> OriginalActionEnabledFunc = new Dictionary<string, Func<IMyTerminalBlock, bool>>();
        public readonly Dictionary<string, Func<IMyTerminalBlock, bool>> OriginalControlVisibleFunc = new Dictionary<string, Func<IMyTerminalBlock, bool>>();

        public const string ATTACHMENT_BASE = "AttachmentBase";
        public const string ATTACHMENT_BASE_TALL = "AttachmentBaseTall";
        public const string ATTACHMENT_TOP = "AttachmentTop";
        public const string ATTACHMENT_TOP_DELETE = "AttachmentTopDelete";

        private readonly List<MyDefinitionId> blockDefIds = new List<MyDefinitionId>()
        {
            new MyDefinitionId(typeof(MyObjectBuilder_MotorAdvancedStator), ATTACHMENT_BASE),
            new MyDefinitionId(typeof(MyObjectBuilder_MotorAdvancedStator), ATTACHMENT_BASE_TALL),
        };

        public override void LoadData()
        {
            Instance = this;
        }

        protected override void UnloadData()
        {
            Instance = null;
        }

        public static bool IsAttachmentBaseBlock(MyDefinitionId defId)
        {
            return Instance.blockDefIds.Contains(defId);
        }
    }
}
