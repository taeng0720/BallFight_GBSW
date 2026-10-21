begin;

alter table public.fighters drop constraint if exists fighters_skill_check;
alter table public.fighters add constraint fighters_skill_check check (skill in ('burst', 'heal', 'shield', 'script', 'army_aura', 'pasta_whip'));
alter table public.fighters add column if not exists blocks_json text not null default '' check (length(blocks_json) <= 100000 and (blocks_json = '' or jsonb_typeof(blocks_json::jsonb) = 'object'));
alter table public.fighters add column if not exists assets_json text not null default '{"items":[]}' check (length(assets_json) <= 700000 and jsonb_typeof(assets_json::jsonb) = 'object');
alter table public.fighters add column if not exists portrait text not null default '' check (length(portrait) <= 40);


alter table public.fighter_requests drop constraint if exists fighter_requests_skill_check;
alter table public.fighter_requests add constraint fighter_requests_skill_check check (skill in ('burst', 'heal', 'shield', 'script', 'army_aura', 'pasta_whip'));
alter table public.fighter_requests add column if not exists blocks_json text not null default '' check (length(blocks_json) <= 100000 and (blocks_json = '' or jsonb_typeof(blocks_json::jsonb) = 'object'));
alter table public.fighter_requests add column if not exists assets_json text not null default '{"items":[]}' check (length(assets_json) <= 700000 and jsonb_typeof(assets_json::jsonb) = 'object');
alter table public.fighter_requests add column if not exists portrait text not null default '' check (length(portrait) <= 40);

grant insert(blocks_json, assets_json, portrait) on public.fighter_requests to authenticated;
create or replace function public.review_fighter(request_id text, decision text, note text, payload jsonb)
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
commit;
