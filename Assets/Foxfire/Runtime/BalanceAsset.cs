using UnityEngine;
namespace Foxfire
{
[CreateAssetMenu(menuName = "Foxfire/Balance", fileName = "DefaultBalance")]
public sealed class BalanceAsset : ScriptableObject
{
    public Balance values = new Balance();
}
}
