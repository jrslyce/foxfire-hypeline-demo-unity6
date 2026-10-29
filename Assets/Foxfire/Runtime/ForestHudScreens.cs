using System;
using UnityEngine;

namespace Foxfire
{
internal sealed partial class ForestHud
{
    GUIStyle screenTitle, screenSubtitle, sectionTitle, reportRow, reportCount, thankYouName,
        screenFootnote, metricValue, metricCaption, noticeHeading, noticeText;
    int reportRun = -1, reportPage = -1;
    string[] sentCounts, playedCounts, visibleNames;
    string reportTotals, runTime, runKills, runUpgrades, runViewers, pageLabel;

    void CreateScreenStyles(Font font)
    {
        screenTitle = Style(font, 28, Ivory, FontStyle.Bold);
        screenSubtitle = Style(font, 15, Ivory);
        sectionTitle = Style(font, 12, Teal, FontStyle.Bold);
        reportRow = Style(font, 17, Ivory);
        reportCount = Style(font, 17, Color.white, FontStyle.Bold, TextAnchor.MiddleRight);
        thankYouName = Style(font, 16, Ivory);
        screenFootnote = Style(font, 12, new Color(.69f, .79f, .75f));
        metricValue = Style(font, 25, Ivory, FontStyle.Bold);
        metricCaption = Style(font, 10, Teal, FontStyle.Bold);
        noticeHeading = Style(font, 11, Teal, FontStyle.Bold);
        noticeText = Style(font, 18, Ivory, FontStyle.Bold);
        sentCounts = new string[Actions.Count];
        playedCounts = new string[Actions.Count];
        visibleNames = new string[Simulation.ThankYouNamesPerPage];
    }

    public void DrawNotices(Simulation game, bool simulator)
    {
        if (!game.Active) return;
        float top = (simulator ? 548 : 680) - game.Notices.Count * 58;
        for (int i = 0; i < game.Notices.Count; i++)
        {
            string text = game.Notices[i];
            bool upgrade = text.StartsWith("Upgrade: ", StringComparison.Ordinal);
            float y = top + i * 58;
            Panel(new Rect(426, y, 428, 51));
            GUI.color = Teal;
            GUI.DrawTexture(new Rect(447, y + 14, 24, 24), sparkle);
            GUI.color = Color.white;
            Label(new Rect(485, y + 7, 342, 14), upgrade ? "LEVEL UP" : "THE CLEARING", noticeHeading);
            Fit(new Rect(485, y + 21, 342, 24), upgrade ? text.Substring(9) : text, noticeText, 18, 12);
        }
    }

    public void DrawIntro(string subtitle, string countdown)
    {
        Panel(new Rect(340, 225, 600, 244));
        if (atlas != null) Region(new Rect(380, 256, 66, 66), FoxSource);
        Label(new Rect(465, 260, 440, 40), "THE FOREST IS CALLING", screenTitle);
        Label(new Rect(380, 336, 530, 28), subtitle, screenSubtitle);
        Label(new Rect(380, 382, 530, 25), countdown, screenSubtitle);
        Label(new Rect(380, 427, 530, 22), "AI-controlled fox · Viewer-powered game", screenFootnote);
    }

    public void DrawResults(Simulation game, string countdown)
    {
        var report = game.RunInteractions;
        if (reportRun != game.RunNumber)
        {
            reportRun = game.RunNumber; reportPage = -1;
            for (int i = 0; i < Actions.Count; i++)
            {
                sentCounts[i] = report.SentCount((ActionKind)i).ToString();
                playedCounts[i] = report.PlayedCount((ActionKind)i).ToString();
            }
            reportTotals = report.TotalSent + " sent  ·  " + report.TotalPlayed + " played  ·  " +
                (report.TotalSent - report.TotalPlayed) + " not played";
            runTime = ((int)game.Time / 60) + ":" + ((int)game.Time % 60).ToString("D2");
            runKills = game.Kills.ToString(); runUpgrades = game.Level.ToString();
            runViewers = report.ViewerCount.ToString();
        }
        int page = Math.Min(game.ThankYouPages - 1,
            (int)(game.StateTime / (Mathf.Max(.01f, game.ResultsDuration) / game.ThankYouPages)));
        if (page != reportPage)
        {
            reportPage = page;
            for (int i = 0; i < visibleNames.Length; i++)
            {
                int index = page * visibleNames.Length + i;
                visibleNames[i] = index < report.ViewerCount ? report.ViewerAt(index) : null;
            }
            pageLabel = game.ThankYouPages > 1 ? "VIEWERS  " + (page + 1) + " / " + game.ThankYouPages : "";
        }

        Rounded(new Rect(0, 0, 1280, 720), new Color(0, .015f, .015f, .46f), 0);
        Panel(new Rect(222, 132, 836, 548));
        if (atlas != null) Region(new Rect(258, 155, 58, 58), FoxSource);
        string title = game.Outcome == Outcome.Victory ? "THE CLEARING RESTORED" :
            game.Outcome == Outcome.Defeat ? "A GUARDIAN FALLS" : "THE FOREST ENDURES";
        Label(new Rect(334, 154, 675, 40), title, screenTitle);
        Label(new Rect(334, 192, 675, 24), game.Outcome == Outcome.Victory ? "Victory · The clearing is safe once more." :
            game.Outcome == Outcome.Defeat ? "Defeat · Thank you for standing with our guardian." :
            "Run complete · Thank you for shaping the clearing.", screenSubtitle);

        Metric(264, "SURVIVED", runTime);
        Metric(445, "ENEMIES DEFEATED", runKills);
        Metric(643, "UPGRADES", runUpgrades);
        Metric(845, "VIEWERS", runViewers);
        Rule(new Rect(264, 287, 750, 1));
        Label(new Rect(264, 300, 270, 24), "VIEWER INTERACTIONS", sectionTitle);
        Label(new Rect(734, 300, 280, 24), "THANK YOU, VIEWERS", sectionTitle);
        sectionTitle.alignment = TextAnchor.MiddleRight;
        Label(new Rect(540, 300, 64, 24), "SENT", sectionTitle);
        Label(new Rect(621, 300, 64, 24), "PLAYED", sectionTitle);
        sectionTitle.alignment = TextAnchor.MiddleLeft;
        Rule(new Rect(709, 308, 1, 276));

        int row = 0;
        for (int i = 0; i < Actions.Count; i++)
        {
            if (report.SentCount((ActionKind)i) == 0) continue;
            float y = 339 + row++ * 27;
            Label(new Rect(264, y, 270, 26), Actions.Label((ActionKind)i), reportRow);
            Label(new Rect(540, y, 64, 26), sentCounts[i], reportCount);
            Label(new Rect(621, y, 64, 26), playedCounts[i], reportCount);
        }
        if (row == 0)
        {
            Label(new Rect(264, 342, 421, 28), "No viewer actions this run.", reportRow);
            Label(new Rect(264, 376, 421, 24), "A fresh chance to join the adventure is coming.", screenFootnote);
        }
        if (report.ViewerCount == 0)
        {
            thankYouName.fontSize = 16;
            Label(new Rect(734, 340, 280, 28), "Thanks for watching!", thankYouName);
            Label(new Rect(734, 372, 280, 24), "Your next chance is just ahead.", screenFootnote);
        }
        for (int i = 0; i < visibleNames.Length && visibleNames[i] != null; i++)
            Fit(new Rect(734, 337 + i * 19, 280, 22), visibleNames[i], thankYouName, 16, 11);
        Label(new Rect(734, 568, 280, 20), pageLabel, screenFootnote);
        Label(new Rect(264, 561, 430, 23), reportTotals, screenFootnote);
        if (report.TotalSent > report.TotalPlayed)
            Label(new Rect(264, 583, 430, 19), "Not played: rejected, expired, or the run ended first.", screenFootnote);
        Rule(new Rect(264, 615, 750, 1));
        screenSubtitle.alignment = TextAnchor.MiddleCenter;
        Label(new Rect(264, 624, 750, 26), countdown, screenSubtitle);
        screenSubtitle.alignment = TextAnchor.MiddleLeft;
        Meter(new Rect(264, 657, 750, 6), 1 - game.StateTime / Mathf.Max(.01f, game.ResultsDuration), Teal);
    }

    void Metric(float x, string caption, string value)
    {
        Label(new Rect(x, 233, 166, 17), caption, metricCaption);
        Label(new Rect(x, 251, 166, 31), value, metricValue);
    }
    static void Rule(Rect rect) { Rounded(rect, new Color(.62f, .46f, .24f, .4f), 0); }
}
}
