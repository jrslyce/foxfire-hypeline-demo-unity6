using System;
using Foxfire;
class TutorialTests
{
    static void Check(bool value, string reason) { if (!value) throw new Exception(reason); }
    static ActionRequest Join(string id, string name) { return new ActionRequest {receiptId=id,sessionId="visit",eventKind="join",displayName=name}; }
    static void Main()
    {
        var game=new Simulation(new Balance());
        var journal=new ReceiptJournal(100);
        using(var dispatcher=new InteractionDispatcher(game,new LocalSimulatorGateway(),journal))
        {
            int index=0;
            foreach(var name in new[]{"Muxy#1","LadyBird","BBQ-fan","RainyStreet"})
            {
                var join=Join("join"+index++,name);
                Check(dispatcher.Submit(join).status=="executed", "join works during intro");
                Check(game.ViewerAnnouncements[game.ViewerAnnouncements.Count-1].viewerName==name,"exact approved name");
                int count=game.ViewerAnnouncements.Count;
                join.expiresAtUnixMs=1;
                Check(dispatcher.Submit(join).status=="executed" && game.ViewerAnnouncements.Count==count,"expired replay preserves outcome without duplicate");
                Check(Actions.ViewerName(name)==name,"punctuation survives sanitizer");
            }
            Check(game.ViewerAnnouncements.Count==3,"banner count bounded");
            Check(game.ExecutedActions==0 && dispatcher.QueueCount==0,"join consumes no gameplay queue or action");
            Check(dispatcher.Submit(Join("forged","bad-name")).status=="rejected","reject unknown demo name");
            var expired=Join("expired","LadyBird");expired.expiresAtUnixMs=1;
            Check(dispatcher.Submit(expired).reason=="request_expired","expired fresh join rejected");
            while(!game.Active) game.Step(1f/30);
            game.HP=1; // Cosmetic is not a hinder action; guardian protection cannot block it.
            Check(dispatcher.Availability(ActionKind.TealCelebration)==null,"cosmetic not guardian restricted");
            float hp=game.HP;int enemies=game.Enemies.Count,shots=game.Shots.Count,hazards=game.Hazards.Count;
            var action=new ActionRequest {receiptId="cosmetic",sessionId="visit",actionId=Actions.Scoped(game.RunId,ActionKind.TealCelebration),displayName="BBQ-fan"};
            Check(dispatcher.Submit(action).status=="queued","join did not consume session cooldown");dispatcher.Tick();
            Check(journal.Find("cosmetic").status=="executed" && game.Celebration==3,"cosmetic executes for three seconds");
            Check(game.HP==hp && game.Enemies.Count==enemies && game.Shots.Count==shots && game.Hazards.Count==hazards,"cosmetic has no combat mutation");
            Check(dispatcher.Cooldown(ActionKind.TealCelebration)==10,"cosmetic cooldown ten seconds");
            int announcements=game.ViewerAnnouncements.Count;
            dispatcher.Submit(action);
            Check(game.ExecutedActions==1 && game.ViewerAnnouncements.Count==announcements,"action replay deduplicated");
            Check(game.ViewerAnnouncements[announcements-1].actionName=="Teal Celebration","structured action banner");
            game.Reset();
            Check(game.ViewerAnnouncements.Count==announcements,"announcements survive round boundary");
            for(int i=0;i<241;i++) game.Step(1f/30);
            Check(game.ViewerAnnouncements.Count==0 && game.Celebration==0,"banners expire independently and effect resets");
        }
        foreach(var name in new[]{"Muxy#1","LadyBird","BBQ-fan","RainyStreet"})
        {
            var namedGame=new Simulation(new Balance());
            namedGame.Execute(ActionKind.OrangeChampion,name);
            Check(namedGame.Enemies.Find(e=>e.champion).displayName==name,"exact champion nameplate identity");
            Check(namedGame.ViewerAnnouncements[0].viewerName==name,"exact action banner identity");
        }
        Check(Actions.Category(ActionKind.TealCelebration)=="cosmetic" && !Actions.Hinder(ActionKind.TealCelebration),"explicit cosmetic category");
        Check(Actions.Category(ActionKind.OrangeChampion)=="hinder" && Actions.Category(ActionKind.LaserMode)=="help","legacy categories preserved");
        Check(Actions.Cooldown(new Balance {actionCooldowns=new float[7]},ActionKind.TealCelebration)==10,"legacy balance cosmetic default");
        Console.WriteLine("PASS tutorial joins, exact names, replay, expiry, announcements, cosmetic invariants and legacy balance");
    }
}
