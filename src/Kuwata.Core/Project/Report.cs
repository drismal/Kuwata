using System.Globalization;
using System.Text;
using Kuwata.Core.Method;

namespace Kuwata.Core.Project;

/// <summary>Текстовый отчёт по случаю.</summary>
public static class Report
{
    public static string Build(KuwataProject p, CaseResult r)
    {
        var ci = CultureInfo.GetCultureInfo("ru-RU");
        var sb = new StringBuilder();
        void L(string s = "") => sb.AppendLine(s);
        string F(double? v, string unit = "°") => v is { } d ? d.ToString("0.0", ci) + unit : "—";
        string Src(ValueSource s) => s switch
        {
            ValueSource.Manual => "вручную",
            ValueSource.Landmarks => "по ориентирам",
            ValueSource.AutoFromScans => "авто по сканам",
            _ => "нет данных",
        };

        L($"СЛУЧАЙ: {(string.IsNullOrWhiteSpace(p.CaseName) ? "без названия" : p.CaseName)}");
        L($"Перенос: {p.Registration}");
        L();
        L("СУСТАВНЫЕ ПАРАМЕТРЫ");
        L($"  ССП справа (C1) {F(p.Joint.SagittalPathRight)}   слева (C2) {F(p.Joint.SagittalPathLeft)}");
        if (r.Angles is { } k)
        {
            L($"  Беннет справа {F(k.BennettRight)}{(k.BennettRightFromHanau ? " (Ханау: H/8+12)" : "")}" +
              $"   слева {F(k.BennettLeft)}{(k.BennettLeftFromHanau ? " (Ханау: H/8+12)" : "")}");
            L("  (угол Беннета в формулы Куваты не входит — нужен для кинематики и настройки артикулятора)");
        }
        L();
        L("ФРОНТАЛЬНАЯ НАПРАВЛЯЮЩАЯ");
        L($"  Overbite {F(r.Overbite.Value, " мм")} ({Src(r.Overbite.Source)})   Overjet {F(r.Overjet.Value, " мм")} ({Src(r.Overjet.Source)})");
        L($"  A3 протрузия {F(r.A3.Value)} ({Src(r.A3.Source)})");
        L($"  A1 вправо {F(r.A1.Value)} ({Src(r.A1.Source)})   A2 влево {F(r.A2.Value)} ({Src(r.A2.Source)})");
        L();
        if (r.Angles is { } a)
        {
            L("НАКЛОНЫ СКАТОВ (Кувата, от горизонтали артикулятора)");
            foreach (var s in new[] { a.Right, a.Left })
            {
                L($"  {s.Side} сторона: WP {F(s.WorkingSlope)}  BP {F(s.BalancingSlope)}  P3 {F(s.ProtrusiveSlope)}  " +
                  $"угол раскрытия {F(s.DeploymentAngle)} → шаблон {KuwataAngles.NearestCuspTemplate(s.DeploymentAngle)}");
                L("     зуб  запас   P3_i   BP_i   WP_i   допуск");
                foreach (var t in s.Teeth)
                    L($"     {t.Tooth}    {t.Margin,4:0.0}  {t.P3,6:0.0} {t.BP,6:0.0} {t.WP,6:0.0}  {t.Tolerance,6:0.0}");
            }
            L("  Правила: наклон внутренних скатов ≤ BP (иначе балансирующие контакты); фактический");
            L("  протрузионный наклон меньше P3; язычные скаты нижних на рабочей стороне положе WP.");
            L();
        }
        if (r.Scheme is { } sc)
        {
            L("ОККЛЮЗИОННАЯ ПЛОСКОСТЬ (Бродрик, P.M.S.)");
            L($"  R = {F(p.Broadrick.RadiusMm, " мм")}, задняя точка {p.Broadrick.PosteriorPoint}, сдвиг OPSC кзади {F(p.Broadrick.OpscShiftMm, " мм")}");
            foreach (var b in new[] { sc.Right, sc.Left })
                L($"  {(b.Side == Articulator.Side.Right ? "Справа" : "Слева")}: OPSC (Y {F(b.Opsc.Y, "")}; Z {F(b.Opsc.Z, "")}) мм");
            L($"  Кривая Уилсона: R = {F(sc.Wilson.Radius, " мм")}, центр (X {F(sc.Wilson.CenterX, "")}; Z {F(sc.Wilson.CenterZ, "")}) в плоскости Y = {F(sc.Wilson.PlaneY, "")}");
            L();
        }
        if (r.Teeth.Count > 0)
        {
            L($"РАЗМЕРЫ ЗУБОВ ({p.Proportions}; от резца {F(p.IncisorWidth, "")} × {F(p.IncisorLength, "")} мм)");
            L("     зуб   ширина  высота  щ-я шир.  бугор-бугор   видимая");
            foreach (var t in r.Teeth)
                L($"     {(t.Upper ? "в" : "н")}{t.Tooth}   {t.MesioDistal,6:0.0}  {t.CrownHeight,6:0.0}  {F(t.BuccoLingual, ""),8}  " +
                  $"{(t.CuspDistanceMin is { } mn ? $"{mn:0.0}–{t.CuspDistanceMax:0.0}" : "—"),11}   {F(t.ApparentWidth, "")}");
            L();
        }
        if (r.Artex is { } x)
        {
            L("НАСТРОЙКА ARTEX CR");
            L($"  ССП R/L {F(x.SagittalPathRight)} / {F(x.SagittalPathLeft)};  Беннет R/L {F(x.BennettRight)} / {F(x.BennettLeft)}");
            L($"  Резцовый столик: протрузия {F(x.IncisalTableProtrusive)}, латерально R/L {F(x.IncisalTableLateralRight)} / {F(x.IncisalTableLateralLeft)}");
            L($"  Ретрузия / long centric {F(x.RetrusionLongCentricMm, " мм")}");
            L();
        }
        if (r.Warnings.Count > 0)
        {
            L("ПРЕДУПРЕЖДЕНИЯ");
            foreach (var w in r.Warnings) L("  • " + w);
            L();
        }
        if (r.Errors.Count > 0)
        {
            L("НЕ РАССЧИТАНО");
            foreach (var e in r.Errors) L("  • " + e);
        }
        return sb.ToString();
    }
}
