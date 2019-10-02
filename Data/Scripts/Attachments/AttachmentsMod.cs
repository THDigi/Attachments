using System.Collections.Generic;
using Sandbox.Common.ObjectBuilders;
using VRage.Game;
using VRage.Game.Components;

namespace Digi.Attachments
{
    [MySessionComponentDescriptor(MyUpdateOrder.NoUpdate)]
    public class AttachmentsMod : MySessionComponentBase
    {
        public static AttachmentsMod Instance = null;

        public bool ParsedTerminalControls = false;

        public const string ATTACHMENT_BASE_SMALL = "AttachmentBaseSmall";
        public const string ATTACHMENT_BASE_LARGE = "AttachmentBase";
        public const string ATTACHMENT_BASE_LARGE_TALL = "AttachmentBaseTall";
        public const string ATTACHMENT_TOP_SMALL = "AttachmentTop";
        public const string ATTACHMENT_TOP_LARGE = "AttachmentTopLarge";

        private readonly List<MyDefinitionId> baseDefIds = new List<MyDefinitionId>()
        {
            new MyDefinitionId(typeof(MyObjectBuilder_MotorAdvancedStator), ATTACHMENT_BASE_LARGE),
            new MyDefinitionId(typeof(MyObjectBuilder_MotorAdvancedStator), ATTACHMENT_BASE_SMALL),
            new MyDefinitionId(typeof(MyObjectBuilder_MotorAdvancedStator), ATTACHMENT_BASE_LARGE_TALL),
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
            return Instance.baseDefIds.Contains(defId);
        }
    }
}
