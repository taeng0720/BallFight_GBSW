const { PGlite } = require('@electric-sql/pglite');
const fs = require('fs');
const path = require('path');
const assert = require('assert/strict');

async function main() {
  const db = new PGlite();
  await db.exec(`
    create role anon;
    create role authenticated;
    grant usage on schema public to anon, authenticated;
  `);
  const schema = fs.readFileSync(path.join(__dirname, '../../Backend/schema.sql'), 'utf8');
  await db.exec(schema);
  await db.exec(schema);

  const deny = async (sql, values = []) => {
    let rejected = false;
    try { await db.query(sql, values); } catch { rejected = true; }
    assert.ok(rejected, 'Unexpectedly allowed: ' + sql);
  };
  const insertIdea = (name, author, description) =>
    db.query('insert into public.ideas(name, author, description) values ($1, $2, $3) returning *', [name, author, description]);

  await db.exec('set role anon');
  const fighters = (await db.query('select * from public.fighters order by id')).rows;
  assert.deepEqual(fighters.map(f => f.id), ['gu-bonyun', 'jang-heecheol']);
  assert.equal(fighters[1].hp, 300); assert.equal(fighters[0].hp, 250); assert.equal(fighters[0].skill, 'pasta_whip');
  await deny("insert into public.fighters(name,hp,attack,speed,skill,skill_power,cooldown,color) values ('hack',140,15,230,'burst',25,5,'#FF785A')");
  await deny("update public.fighters set hp = 1000");

  const created = (await insertIdea('  불꽃 공  ', ' 익명 ', '  닿으면 화상 피해를 주는 공  ')).rows[0];
  assert.equal(created.name, '불꽃 공'); assert.equal(created.author, '익명'); assert.equal(created.description, '닿으면 화상 피해를 주는 공');
  assert.ok(created.id && created.created_at);
  await insertIdea('얼음 공', '', '맞으면 잠깐 얼어붙음');
  assert.equal((await db.query('select * from public.ideas')).rows.length, 2);

  await deny("insert into public.ideas(name, author, description) values ('', '', '설명이 충분히 깁니다')");
  await deny("insert into public.ideas(name, author, description) values ('이름', '', '짧음')");
  await deny("insert into public.ideas(name, author, description) values ($1, '', '설명이 충분히 깁니다')", ['가'.repeat(25)]);
  await deny("insert into public.ideas(name, author, description) values ('이름', $1, '설명이 충분히 깁니다')", ['가'.repeat(17)]);
  await deny("insert into public.ideas(name, author, description) values ('이름', '', $1)", ['가'.repeat(301)]);
  await deny("insert into public.ideas(name, author, description) values ('불꽃 공', '', '닿으면 화상 피해를 주는 공')");
  await deny("insert into public.ideas(id, name, author, description) values (gen_random_uuid(), '아이디 지정', '', '설명이 충분히 깁니다')");
  await deny("insert into public.ideas(name, author, description, created_at) values ('날짜 조작', '', '설명이 충분히 깁니다', now() - interval '1 year')");
  await deny("update public.ideas set description = '수정된 설명입니다'");
  await deny('delete from public.ideas');
  await deny('select public.ideas_guard()');

  for (let i = 0; i < 28; i++) await insertIdea('공 ' + i, '', '도배 방지 확인용 아이디어 ' + i);
  await deny("insert into public.ideas(name, author, description) values ('31번째', '', '1분 제한을 넘는 아이디어')");
  assert.equal((await db.query('select count(*)::int as total from public.ideas')).rows[0].total, 30);

  await db.exec('reset role');
  console.log('PASS: idempotent schema, read-only fighters catalog, idea insert/trim/validation, duplicate and flood guard, no update/delete');
  await db.close();
}
main().catch(error => { console.error(error); process.exitCode = 1; });
