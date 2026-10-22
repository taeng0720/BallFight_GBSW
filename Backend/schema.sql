begin;

create table if not exists public.fighters (
    id text primary key default gen_random_uuid()::text,
    name text not null check (length(trim(name)) between 1 and 24),
    hp integer not null check (hp between 30 and 1000),
    attack integer not null check (attack between 1 and 100),
    speed real not null check (speed between 100 and 500),
    skill text not null check (skill in ('burst', 'heal', 'shield', 'script', 'army_aura', 'pasta_whip')),
    skill_power integer not null check (skill_power between 1 and 150),
    cooldown real not null check (cooldown between 2 and 20),
    color text not null check (color ~ '^#[0-9a-fA-F]{6}$'),
    blocks_json text not null default '' check (length(blocks_json) <= 100000),
    assets_json text not null default '{"items":[]}' check (length(assets_json) <= 700000),
    portrait text not null default '' check (length(portrait) <= 40),
    revision integer not null default 1,
    updated_at timestamptz not null default now()
);
alter table public.fighters enable row level security;
revoke all on public.fighters from anon, authenticated;
grant select on public.fighters to anon, authenticated;
drop policy if exists fighters_read on public.fighters;
create policy fighters_read on public.fighters for select to anon, authenticated using (true);

insert into public.fighters (id, name, hp, attack, speed, skill, skill_power, cooldown, color) values
    ('jang-heecheol', '장희철', 300, 5, 300, 'army_aura', 6, 3, '#728970'),
    ('gu-bonyun', '구본윤', 250, 4, 330, 'pasta_whip', 22, 2, '#D6A653')
on conflict (id) do nothing;

create table if not exists public.ideas (
    id uuid primary key default gen_random_uuid(),
    name text not null check (length(name) between 1 and 24),
    author text not null default '' check (length(author) <= 16),
    description text not null check (length(description) between 5 and 300),
    created_at timestamptz not null default now()
);
create index if not exists ideas_created_at on public.ideas (created_at desc);
alter table public.ideas enable row level security;
revoke all on public.ideas from anon, authenticated;
grant select on public.ideas to anon, authenticated;
grant insert (name, author, description) on public.ideas to anon, authenticated;
drop policy if exists ideas_read on public.ideas;
create policy ideas_read on public.ideas for select to anon, authenticated using (true);
drop policy if exists ideas_write on public.ideas;
create policy ideas_write on public.ideas for insert to anon, authenticated with check (true);

create or replace function public.ideas_guard() returns trigger
language plpgsql security definer set search_path = ''
as $$
begin
    new.name := btrim(coalesce(new.name, ''));
    new.author := btrim(coalesce(new.author, ''));
    new.description := btrim(coalesce(new.description, ''));
    new.created_at := now();
    if (select count(*) from public.ideas where created_at > now() - interval '1 minute') >= 30 then
        raise exception '아이디어가 너무 많이 올라오고 있어요. 잠시 후 다시 시도해 주세요.' using errcode = 'P0001';
    end if;
    if exists (select 1 from public.ideas where name = new.name and description = new.description and created_at > now() - interval '1 day') then
        raise exception '같은 아이디어가 이미 등록되어 있어요.' using errcode = 'P0001';
    end if;
    return new;
end;
$$;
revoke all on function public.ideas_guard() from public, anon, authenticated;
drop trigger if exists ideas_guard on public.ideas;
create trigger ideas_guard before insert on public.ideas for each row execute function public.ideas_guard();

commit;
