begin;

update public.fighters
set attack = 4, skill_power = 22, revision = revision + 1, updated_at = now()
where id = 'gu-bonyun' and name = '구본윤' and skill = 'pasta_whip'
    and attack = 5 and speed = 330 and skill_power = 32;

commit;
