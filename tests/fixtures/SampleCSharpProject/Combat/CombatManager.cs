namespace SampleGame.Combat
{
    public class CombatManager : IDamageable
    {
        private readonly HitBox _hitBox = new HitBox();

        public void ApplyDamage(int amount)
        {
            _hitBox.Damage += amount;
        }
    }
}
