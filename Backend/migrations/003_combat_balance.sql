begin;

update public.fighters
set attack = 5, speed = 300, skill_power = 6, revision = revision + 1, updated_at = now()
where id = 'jang-heecheol' and name = '장희철' and skill = 'army_aura'
    and attack = 3 and speed = 220 and skill_power = 3;

update public.fighters
set attack = 5, speed = 330, skill_power = 32, revision = revision + 1, updated_at = now()
where id = 'gu-bonyun' and name = '구본윤' and skill = 'pasta_whip'
    and attack = 7 and speed = 240 and skill_power = 48;

commit;
