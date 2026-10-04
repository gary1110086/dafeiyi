using System.Drawing;
using System.Windows.Forms;
namespace LightTranslate {
    internal static class TrayTheme {
        internal static void Apply(ContextMenuStrip menu) {
            menu.Renderer=new Renderer(); menu.BackColor=Color.FromArgb(23,38,62); menu.ForeColor=Color.FromArgb(222,232,244);
            menu.Font=new Font("Microsoft YaHei UI",9); menu.Padding=new Padding(6); menu.ShowImageMargin=false; menu.ShowCheckMargin=true;
            menu.Opening+=delegate { Style(menu.Items); };
        }
        static void Style(ToolStripItemCollection items) {
            foreach(ToolStripItem item in items) { item.ForeColor=Color.FromArgb(222,232,244); item.Padding=new Padding(8,6,8,6); var sub=item as ToolStripMenuItem; if(sub!=null) { sub.DropDown.BackColor=Color.FromArgb(23,38,62); sub.DropDown.ForeColor=item.ForeColor; Style(sub.DropDownItems); } }
        }
        sealed class Renderer:ToolStripProfessionalRenderer {
            internal Renderer():base(new Palette()) { }
            protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e) { e.ArrowColor=Color.FromArgb(140,211,223); base.OnRenderArrow(e); }
        }
        sealed class Palette:ProfessionalColorTable {
            readonly Color background=Color.FromArgb(23,38,62),hover=Color.FromArgb(41,71,96),border=Color.FromArgb(66,96,125);
            public override Color ToolStripDropDownBackground { get { return background; } }
            public override Color ImageMarginGradientBegin { get { return background; } }
            public override Color ImageMarginGradientMiddle { get { return background; } }
            public override Color ImageMarginGradientEnd { get { return background; } }
            public override Color MenuItemSelected { get { return hover; } }
            public override Color MenuItemBorder { get { return hover; } }
            public override Color MenuItemSelectedGradientBegin { get { return hover; } }
            public override Color MenuItemSelectedGradientEnd { get { return hover; } }
            public override Color MenuItemPressedGradientBegin { get { return hover; } }
            public override Color MenuItemPressedGradientMiddle { get { return hover; } }
            public override Color MenuItemPressedGradientEnd { get { return hover; } }
            public override Color MenuBorder { get { return border; } }
            public override Color SeparatorDark { get { return border; } }
            public override Color SeparatorLight { get { return background; } }
            public override Color CheckBackground { get { return hover; } }
            public override Color CheckSelectedBackground { get { return hover; } }
            public override Color CheckPressedBackground { get { return hover; } }
        }
    }
}
