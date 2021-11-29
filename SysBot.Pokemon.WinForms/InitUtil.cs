#if NETFRAMEWORK
using PKHeX.Core;
using PKHeX.Drawing.PokeSprite;

namespace SysBot.Pokemon.WinForms
{
    public static class InitUtil
    {
        public static void InitializeStubs()
        {
            var sav8 = new SAV8SWSH();
            SetUpSpriteCreator(sav8);
        }

        private static void SetUpSpriteCreator(SaveFile sav)
        {
            SpriteUtil.Initialize(sav);
        }
    }
}
#endif
