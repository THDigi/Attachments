using System;
using Sandbox.ModAPI;

namespace Digi
{
    public class CombineFunc
    {
        private readonly Func<IMyTerminalBlock, bool> originalFunc;
        private readonly Func<IMyTerminalBlock, bool> customFunc;

        private CombineFunc(Func<IMyTerminalBlock, bool> originalFunc, Func<IMyTerminalBlock, bool> customFunc)
        {
            this.originalFunc = originalFunc;
            this.customFunc = customFunc;
        }

        private bool ResultFunc(IMyTerminalBlock block)
        {
            if(block?.CubeGrid == null)
                return false;

            bool originalCondition = (originalFunc == null ? true : originalFunc.Invoke(block));
            bool customCondition = (customFunc == null ? true : customFunc.Invoke(block));

            return originalCondition && customCondition;
        }

        public static Func<IMyTerminalBlock, bool> Create(Func<IMyTerminalBlock, bool> originalFunc, Func<IMyTerminalBlock, bool> customFunc)
        {
            return new CombineFunc(originalFunc, customFunc).ResultFunc;
        }
    }
}
