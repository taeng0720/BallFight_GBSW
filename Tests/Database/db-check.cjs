const { PGlite } = require('@electric-sql/pglite');
const fs = require('fs');
const path = require('path');
const assert = require('assert/strict');

async function main() {
  const db = new PGlite();
  const creator = '00000000-0000-0000-0000-000000000001';
  const admin = '00000000-0000-0000-0000-000000000002';
  const other = '00000000-0000-0000-0000-000000000003';
  await db.exec(`
    create role anon;
    create role authenticated;
    create schema auth;
    create table auth.users(id uuid primary key);
    create function auth.uid() returns uuid language sql stable as
      $$ select nullif(current_setting('request.jwt.claim.sub', true), '')::uuid $$;
    grant usage on schema public, auth to anon, authenticated;
    grant execute on function auth.uid() to anon, authenticated;
    insert into auth.users values ('${creator}'), ('${admin}'), ('${other}');
  `);
  await db.exec(fs.readFileSync(path.join(__dirname, '../../Backend/schema.sql'), 'utf8'));
  if (process.env.HBF_TEST_MIGRATION === '1') {
    await db.exec("delete from public.fighters; insert into public.fighters(id,name,hp,attack,speed,skill,skill_power,cooldown,color) values ('ember','엠버',120,16,270,'burst',24,5,'#FF785A'), ('mint','민트',150,10,240,'heal',22,6,'#5CE0C1'), ('orbit','오르빗',180,12,200,'shield',30,7,'#A59AFF');");
    for (const table of ['fighters', 'fighter_requests']) {
      await db.exec(`alter table public.${table} drop column blocks_json, drop column assets_json, drop column portrait;
        alter table public.${table} drop constraint ${table}_skill_check;
        alter table public.${table} add constraint ${table}_skill_check check (skill in ('burst','heal','shield'));`);
    }
    const migration = fs.readFileSync(path.join(__dirname, '../../Backend/migrations/001_block_creator.sql'), 'utf8');
    await db.exec(migration);
    await db.exec(migration);
    const fightersMigration = fs.readFileSync(path.join(__dirname, '../../Backend/migrations/002_duel_fighters.sql'), 'utf8');
    await db.exec(fightersMigration);
    await db.exec(fightersMigration);
    const balanceMigration = fs.readFileSync(path.join(__dirname, '../../Backend/migrations/003_combat_balance.sql'), 'utf8');
    await db.exec(balanceMigration);
    await db.exec(balanceMigration);
    const pastaMigration = fs.readFileSync(path.join(__dirname, '../../Backend/migrations/004_pasta_knockback_balance.sql'), 'utf8');
    await db.exec(pastaMigration);
    await db.exec(pastaMigration);
  }
  const initialCount = process.env.HBF_TEST_MIGRATION === '1' ? 5 : 2;
  await db.query('insert into public.admin_accounts values ($1)', [admin]);
  const asUser = async id => {
    await db.exec('reset role');
    await db.query("select set_config('request.jwt.claim.sub', $1, false)", [id || '']);
    await db.exec(id ? 'set role authenticated' : 'set role anon');
  };
  const deny = async (sql, values = []) => {
    let rejected = false;
    try { await db.query(sql, values); } catch { rejected = true; }
    assert.ok(rejected, 'Unexpectedly allowed: ' + sql);
  };
  const payload = { name: '검증 파이터', hp: 140, attack: 15, speed: 230, skill: 'burst', skill_power: 25, cooldown: 5, color: '#FF785A' };
  const submit = async (target = null, revision = 0) => {
    await db.query(`insert into public.fighter_requests(name,hp,attack,speed,skill,skill_power,cooldown,color,target_id,base_revision)
      values($1,140,15,230,'burst',25,5,'#FF785A',$2,$3)`, [payload.name, target, revision]);
    const response = await db.query("select id from public.fighter_requests where status='pending' order by created_at desc");
    return response.rows[0].id;
  };
  await asUser(null);
  assert.equal((await db.query('select * from public.fighters')).rows.length, initialCount);
  assert.equal((await db.query("select count(*)::int as total from public.fighters where name in ('장희철','구본윤')")).rows[0].total, 2);
  const seedJang = (await db.query("select * from public.fighters where id='jang-heecheol'")).rows[0];
  const seedGu = (await db.query("select * from public.fighters where id='gu-bonyun'")).rows[0];
  assert.equal(seedJang.attack, 5); assert.equal(seedJang.speed, 300); assert.equal(seedJang.skill_power, 6);
  assert.equal(seedGu.attack, 4); assert.equal(seedGu.speed, 330); assert.equal(seedGu.skill_power, 22);
  await deny('select * from public.fighter_requests');
  await deny('select public.is_admin()');
  await asUser(creator);
  assert.equal((await db.query('select public.is_admin() as value')).rows[0].value, false);
  const request = await submit();
  assert.equal((await db.query('select * from public.fighters')).rows.length, initialCount);
  await deny("update public.fighter_requests set status='approved'");
  await deny('insert into public.admin_accounts values ($1)', [creator]);
  await deny("insert into public.fighter_requests(name,hp,attack,speed,skill,skill_power,cooldown,color,status) values ('hack',140,15,230,'burst',25,5,'#FF785A','approved')");
  await deny("select public.review_fighter($1,'approved','',$2)", [request, payload]);
  await deny("insert into public.fighters(name,hp,attack,speed,skill,skill_power,cooldown,color) values ('hack',140,15,230,'burst',25,5,'#FF785A')");
  await deny("insert into public.fighter_requests(name,hp,attack,speed,skill,skill_power,cooldown,color,target_id) values ('hack',140,15,230,'burst',25,5,'#FF785A','jang-heecheol')");
  await deny("insert into public.fighter_requests(name,hp,attack,speed,skill,skill_power,cooldown,color) values ('hack',140,15,'NaN','burst',25,5,'#FF785A')");
  await deny("insert into public.fighter_requests(name,hp,attack,speed,skill,skill_power,cooldown,color,blocks_json) values ('hack',140,15,230,'script',25,5,'#FF785A','invalid')");
  await asUser(other);
  assert.equal((await db.query('select * from public.fighter_requests')).rows.length, 0);
  const rejectedRequest = await submit();
  await asUser(admin);
  assert.equal((await db.query('select public.is_admin() as value')).rows[0].value, true);
  await db.query("select public.review_fighter($1,'approved','승인',$2)", [request, payload]);
  assert.equal((await db.query('select * from public.fighters')).rows.length, initialCount + 1);
  await deny("select public.review_fighter($1,'approved','',$2)", [request, payload]);
  await db.query("select public.review_fighter($1,'rejected','조정 필요',$2)", [rejectedRequest, payload]);
  assert.equal((await db.query('select * from public.fighters')).rows.length, initialCount + 1);
  const patch1 = await submit('jang-heecheol', seedJang.revision);
  const patch2 = await submit('jang-heecheol', seedJang.revision);
  await db.query("select public.review_fighter($1,'approved','패치',$2)", [patch1, { ...payload, attack: 17 }]);
  const ember = (await db.query("select * from public.fighters where id='jang-heecheol'")).rows[0];
  assert.equal(ember.revision, seedJang.revision + 1);
  assert.equal(ember.attack, 17);
  await deny("select public.review_fighter($1,'approved','오래된 패치',$2)", [patch2, payload]);
  assert.equal((await db.query('select * from public.fighter_history')).rows.length, 2);
  const blockPayload = { ...payload, skill: 'script', blocks_json: JSON.stringify({ version: 1, events: { OnSkill: [] } }), assets_json: '{"items":[]}', portrait: 'star' };
  const blockRequest = await submit();
  await db.query("select public.review_fighter($1,'approved','블록 승인',$2)", [blockRequest, blockPayload]);
  const published = (await db.query("select * from public.fighters where portrait='star'")).rows[0];
  assert.equal(published.skill, 'script');
  assert.equal(published.blocks_json, blockPayload.blocks_json);
  assert.equal(published.assets_json, blockPayload.assets_json);
  await asUser(creator);
  assert.equal((await db.query('select * from public.fighter_requests')).rows.length, 1);
  assert.equal((await db.query('select * from public.fighter_history')).rows.length, 0);
  await db.exec('reset role');
  await db.exec(fs.readFileSync(path.join(__dirname, '../../Backend/migrations/003_combat_balance.sql'), 'utf8'));
  assert.equal((await db.query("select attack from public.fighters where id='jang-heecheol'")).rows[0].attack, 17);
  await db.exec("update public.fighters set attack=9 where id='gu-bonyun'");
  await db.exec(fs.readFileSync(path.join(__dirname, '../../Backend/migrations/004_pasta_knockback_balance.sql'), 'utf8'));
  assert.equal((await db.query("select attack from public.fighters where id='gu-bonyun'")).rows[0].attack, 9);
  console.log('PASS: schema, public catalog, ownership, admin authorization, approval, rejection, validation, duplicate review, revision conflicts, history');
  await db.close();
}
main().catch(error => { console.error(error); process.exitCode = 1; });
