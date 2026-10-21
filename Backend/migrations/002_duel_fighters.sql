begin;

alter table public.fighters drop constraint if exists fighters_skill_check;
alter table public.fighters add constraint fighters_skill_check check (skill in ('burst', 'heal', 'shield', 'script', 'army_aura', 'pasta_whip'));
alter table public.fighter_requests drop constraint if exists fighter_requests_skill_check;
alter table public.fighter_requests add constraint fighter_requests_skill_check check (skill in ('burst', 'heal', 'shield', 'script', 'army_aura', 'pasta_whip'));

insert into public.fighters(id, name, hp, attack, speed, skill, skill_power, cooldown, color) values
('jang-heecheol', '장희철', 640, 3, 220, 'army_aura', 3, 3, '#728970'),
('gu-bonyun', '구본윤', 560, 7, 240, 'pasta_whip', 48, 2, '#D6A653')
on conflict (id) do nothing;

commit;
