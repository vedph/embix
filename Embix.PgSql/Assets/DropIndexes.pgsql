-- Drops Embix secondary indexes and constraints before a bulk load.
-- They are recreated by Indexes.pgsql. Primary keys are preserved.
ALTER TABLE eix_occurrence DROP CONSTRAINT IF EXISTS eix_occurrence_token_fk;
ALTER TABLE eix_occurrence DROP CONSTRAINT IF EXISTS eix_occurrence_fk;
DROP INDEX IF EXISTS eix_occurrence_token_field_target_idx;
DROP INDEX IF EXISTS eix_occurrence_target_field_idx;
DROP INDEX IF EXISTS eix_occurrence_field_idx;
DROP INDEX IF EXISTS eix_token_value_language_uix;
DROP INDEX IF EXISTS eix_token_value_pattern_idx;
DROP INDEX IF EXISTS eix_token_value_trgm_idx;
DROP INDEX IF EXISTS eix_token_value_idx;
