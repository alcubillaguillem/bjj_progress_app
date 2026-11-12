-- BJJ Journal: un documento JSON por usuario (el diario completo), sincronizado desde la app.
create table if not exists public.bjj_journals (
  user_id uuid primary key default auth.uid() references auth.users(id) on delete cascade,
  data jsonb not null,
  updated_at timestamptz not null default now()
);

alter table public.bjj_journals enable row level security;

create policy "bjj_journals_select_own" on public.bjj_journals
  for select to authenticated using ((select auth.uid()) = user_id);
create policy "bjj_journals_insert_own" on public.bjj_journals
  for insert to authenticated with check ((select auth.uid()) = user_id);
create policy "bjj_journals_update_own" on public.bjj_journals
  for update to authenticated using ((select auth.uid()) = user_id) with check ((select auth.uid()) = user_id);
create policy "bjj_journals_delete_own" on public.bjj_journals
  for delete to authenticated using ((select auth.uid()) = user_id);

revoke all on public.bjj_journals from anon;
grant select, insert, update, delete on public.bjj_journals to authenticated;
