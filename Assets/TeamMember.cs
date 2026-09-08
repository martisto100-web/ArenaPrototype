using UnityEngine;

public enum Team
{
    Player,
    Enemy
}

// ATTACH THIS TO: any vehicle root that belongs to a side (PlayerCar, EnemyCar).
// Projectiles copy their shooter's team and pass straight through anything on the
// same team — including whoever fired them. Objects with no TeamMember (the target
// dummy, destructible props) are neutral and can be hit by anyone.
public class TeamMember : MonoBehaviour
{
    public Team team = Team.Player;
}
