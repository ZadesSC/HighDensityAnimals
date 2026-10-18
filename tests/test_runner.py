"""Regression tests for rejecting false-green game runs."""
import importlib.util
from pathlib import Path
import unittest

spec = importlib.util.spec_from_file_location('runtime_runner', Path(__file__).with_name('run.py'))
runner = importlib.util.module_from_spec(spec)
spec.loader.exec_module(runner)


class ResultValidationTests(unittest.TestCase):
    def rows(self):
        return [dict(type='start', expected=['a'], artifactHash='fresh'),
                dict(type='case', id='a', passed=True), dict(type='complete')]

    def test_valid_run(self):
        self.assertEqual(runner.validate_results(self.rows(), {'a'}, 'fresh'), [])

    def test_assertion_failure_cannot_be_hidden_by_complete(self):
        rows = self.rows(); rows[1]['passed'] = False
        self.assertTrue(runner.validate_results(rows, {'a'}, 'fresh'))

    def test_missing_case(self):
        self.assertTrue(runner.validate_results(self.rows()[::2], {'a'}, 'fresh'))

    def test_duplicate_case(self):
        rows = self.rows(); rows.append(rows[1])
        self.assertTrue(runner.validate_results(rows, {'a'}, 'fresh'))

    def test_suite_cannot_shrink_its_own_manifest(self):
        rows = self.rows(); rows[0]['expected'] = []
        self.assertTrue(runner.validate_results(rows, {'a'}, 'fresh'))

    def test_stale_binary(self):
        self.assertTrue(runner.validate_results(self.rows(), {'a'}, 'stale'))

    def test_game_error(self):
        rows = self.rows(); rows.append(dict(type='game_error', text='broken startup'))
        self.assertTrue(runner.validate_results(rows, {'a'}, 'fresh'))

    def test_incomplete_run(self):
        self.assertTrue(runner.validate_results(self.rows()[:-1], {'a'}, 'fresh'))

    def test_caught_harness_exception(self):
        rows = self.rows(); rows.append(dict(type='fatal', error='fixture failed'))
        self.assertTrue(runner.validate_results(rows, {'a'}, 'fresh'))

    def benchmark_rows(self):
        rows = [dict(type='start', expected=['benchmark'], artifactHash='fresh'),
                dict(type='case', id='benchmark', passed=True), dict(type='complete')]
        for pawns, density, machines in [(120, 1, 0), (30, 4, 0), (30, 4, 100)]:
            rows.append(dict(type='benchmark', pawns=pawns, density=density, machines=machines,
                             ticksPerSample=1000, milliseconds=[10.0] * 6, allocations=[100] * 6))
        return rows

    def test_valid_benchmark(self):
        self.assertEqual(runner.validate_results(self.benchmark_rows(), {'benchmark'}, 'fresh'), [])

    def test_missing_benchmark_workload(self):
        self.assertTrue(runner.validate_results(self.benchmark_rows()[:-1], {'benchmark'}, 'fresh'))

    def test_invalid_benchmark_measurement(self):
        rows = self.benchmark_rows(); rows[-1]['milliseconds'][0] = float('nan')
        self.assertTrue(runner.validate_results(rows, {'benchmark'}, 'fresh'))


if __name__ == '__main__': unittest.main()
