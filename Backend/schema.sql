begin;

create table public.admin_accounts (
    user_id uuid primary key references auth.users(id) on delete cascade
);
alter table public.admin_accounts enable row level security;
revoke all on public.admin_accounts from anon, authenticated;

create function public.is_admin() returns boolean
language sql stable security definer set search_path = ''
as $$
    select exists(select 1 from public.admin_accounts where user_id = auth.uid());
$$;
revoke all on function public.is_admin() from public;
grant execute on function public.is_admin() to authenticated;

create table public.fighters (
    id text primary key default gen_random_uuid()::text,
    name text not null check (length(trim(name)) between 1 and 24),
    hp integer not null check (hp between 30 and 1000),
    attack integer not null check (attack between 1 and 100),
    speed real not null check (speed between 100 and 500),
    skill text not null check (skill in ('burst', 'heal', 'shield', 'script', 'army_aura', 'pasta_whip')),
    skill_power integer not null check (skill_power between 1 and 150),
    cooldown real not null check (cooldown between 2 and 20),
    color text not null check (color ~ '^#[0-9a-fA-F]{6}$'),
    blocks_json text not null default '' check (length(blocks_json) <= 100000 and (blocks_json = '' or jsonb_typeof(blocks_json::jsonb) = 'object')),
    assets_json text not null default '{"items":[]}' check (length(assets_json) <= 700000 and jsonb_typeof(assets_json::jsonb) = 'object'),
    portrait text not null default '' check (length(portrait) <= 40),
    revision integer not null default 1,
    updated_at timestamptz not null default now()
);
alter table public.fighters enable row level security;
revoke all on public.fighters from anon, authenticated;
grant select on public.fighters to anon, authenticated;
create policy published_read on public.fighters for select to anon, authenticated using (true);

create table public.fighter_requests (
    id text primary key default gen_random_uuid()::text,
    owner_id uuid not null default auth.uid() references auth.users(id),
    target_id text references public.fighters(id),
    base_revision integer not null default 0,
    name text not null check (length(trim(name)) between 1 and 24),
    hp integer not null check (hp between 30 and 1000),
    attack integer not null check (attack between 1 and 100),
    speed real not null check (speed between 100 and 500),
    skill text not null check (skill in ('burst', 'heal', 'shield', 'script', 'army_aura', 'pasta_whip')),
    skill_power integer not null check (skill_power between 1 and 150),
    cooldown real not null check (cooldown between 2 and 20),
    color text not null check (color ~ '^#[0-9a-fA-F]{6}$'),
    blocks_json text not null default '' check (length(blocks_json) <= 100000 and (blocks_json = '' or jsonb_typeof(blocks_json::jsonb) = 'object')),
    assets_json text not null default '{"items":[]}' check (length(assets_json) <= 700000 and jsonb_typeof(assets_json::jsonb) = 'object'),
    portrait text not null default '' check (length(portrait) <= 40),
    status text not null default 'pending' check (status in ('pending', 'approved', 'rejected')),
    review_note text not null default '' check (length(review_note) <= 1000),
    reviewed_by uuid references auth.users(id),
    created_at timestamptz not null default now(),
    reviewed_at timestamptz,
    published_id text references public.fighters(id)
);
create index fighter_requests_owner on public.fighter_requests(owner_id, created_at desc);
create index fighter_requests_pending on public.fighter_requests(created_at) where status = 'pending';
alter table public.fighter_requests enable row level security;
revoke all on public.fighter_requests from anon, authenticated;
grant select on public.fighter_requests to authenticated;
grant insert(name, hp, attack, speed, skill, skill_power, cooldown, color, target_id, base_revision, blocks_json, assets_json, portrait)
    on public.fighter_requests to authenticated;
create policy requests_read on public.fighter_requests for select to authenticated
    using (owner_id = (select auth.uid()) or (select public.is_admin()));
create policy requests_insert on public.fighter_requests for insert to authenticated
    with check (owner_id = (select auth.uid()) and status = 'pending' and reviewed_by is null
        and (target_id is null or (select public.is_admin())));

create table public.fighter_history (
    id bigint generated always as identity primary key,
    fighter_id text not null references public.fighters(id),
    revision integer not null,
    snapshot jsonb not null,
    reviewer uuid not null references auth.users(id),
    note text not null,
    created_at timestamptz not null default now(),
    unique(fighter_id, revision)
);
alter table public.fighter_history enable row level security;
revoke all on public.fighter_history from anon, authenticated;
grant select on public.fighter_history to authenticated;
create policy history_admin on public.fighter_history for select to authenticated using ((select public.is_admin()));

create function public.review_fighter(request_id text, decision text, note text, payload jsonb)
returns void language plpgsql security definer set search_path = ''
as $$
declare
    proposal public.fighter_requests%rowtype;
    published public.fighters%rowtype;
    target text;
begin
    if not public.is_admin() then raise exception '관리자 권한이 필요합니다.'; end if;
    if decision not in ('approved', 'rejected') or decision is null then raise exception '잘못된 검토 결과입니다.'; end if;
    if note is null or length(note) > 1000 then raise exception '검토 사유는 1000자 이하여야 합니다.'; end if;
    select * into proposal from public.fighter_requests where id = request_id for update;
    if not found or proposal.status <> 'pending' then raise exception '이미 처리되었거나 없는 요청입니다.'; end if;
    if decision = 'approved' then
        if proposal.target_id is not null then
            select * into published from public.fighters where id = proposal.target_id for update;
            if not found or published.revision <> proposal.base_revision then
                raise exception '새 밸런스가 적용되었습니다. 새로고침 후 수정 요청을 다시 만드세요.';
            end if;
            target := proposal.target_id;
            update public.fighters set
                name = trim(payload->>'name'), hp = (payload->>'hp')::integer,
                attack = (payload->>'attack')::integer, speed = (payload->>'speed')::real,
                skill = payload->>'skill', skill_power = (payload->>'skill_power')::integer,
                cooldown = (payload->>'cooldown')::real, color = payload->>'color',
                blocks_json = coalesce(payload->>'blocks_json', ''), assets_json = coalesce(payload->>'assets_json', '{"items":[]}'), portrait = coalesce(payload->>'portrait', ''),
                revision = revision + 1, updated_at = now()
            where id = target returning * into published;
        else
            insert into public.fighters(name, hp, attack, speed, skill, skill_power, cooldown, color, blocks_json, assets_json, portrait)
            values(trim(payload->>'name'), (payload->>'hp')::integer, (payload->>'attack')::integer,
                (payload->>'speed')::real, payload->>'skill', (payload->>'skill_power')::integer,
                (payload->>'cooldown')::real, payload->>'color', coalesce(payload->>'blocks_json', ''), coalesce(payload->>'assets_json', '{"items":[]}'), coalesce(payload->>'portrait', '')) returning * into published;
            target := published.id;
        end if;
        insert into public.fighter_history(fighter_id, revision, snapshot, reviewer, note)
            values(target, published.revision, to_jsonb(published), auth.uid(), note);
    end if;
    update public.fighter_requests set status = decision, review_note = note,
        reviewed_by = auth.uid(), reviewed_at = now(), published_id = target where id = request_id;
end;
$$;
revoke all on function public.review_fighter(text, text, text, jsonb) from public;
grant execute on function public.review_fighter(text, text, text, jsonb) to authenticated;

insert into public.fighters(id, name, hp, attack, speed, skill, skill_power, cooldown, color) values
('jang-heecheol', '장희철', 640, 5, 300, 'army_aura', 6, 3, '#728970'),
('gu-bonyun', '구본윤', 560, 4, 330, 'pasta_whip', 22, 2, '#D6A653');

commit;
