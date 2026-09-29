import json
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path
from scripts.agent_workflow.runner import Engine, WorkflowError


class WorkflowTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.root = Path(self.tmp.name)
        subprocess.run(['git', 'init', '-q', str(self.root)], check=True)
        self.engine = Engine(self.root)
        self.plan = {'max_calls': 8, 'timeout_seconds': 5, 'max_attempts': 2, 'tasks': [
            {'id': 'base', 'priority': 2, 'dependencies': [], 'allowed_files': ['base.txt'], 'acceptance_criteria': ['works']},
            {'id': 'later', 'priority': 1, 'dependencies': ['base'], 'allowed_files': ['later.txt'], 'acceptance_criteria': ['uses base']}]}

    def fake(self, body):
        path = self.root / '.git' / 'fake.py'
        path.write_text(body, encoding='utf-8')
        return [sys.executable, str(path)]

    def init(self):
        self.engine.initialize(self.plan)

    def execute(self, body='print(\'{"session_id":"dev-1","result":"done"}\')', stage='development'):
        return self.engine.run('base', stage, self.fake(body))

    def test_dependency_priority_and_no_false_completion(self):
        self.init()
        self.assertEqual('base', self.engine.next_task()['id'])
        self.execute()
        self.assertEqual('verification', self.engine.status()['tasks'][0]['stage'])
        with self.assertRaises(WorkflowError): self.engine.finish('base')

    def test_invalid_plans(self):
        for field, value in [('allowed_files', ['../escape']), ('dependencies', ['missing'])]:
            plan = json.loads(json.dumps(self.plan)); plan['tasks'][0][field] = value
            with self.assertRaises(WorkflowError): self.engine.initialize(plan)
        self.plan['tasks'][0]['dependencies'] = ['later']
        with self.assertRaises(WorkflowError): self.init()

    def test_failure_timeout_and_malformed_output(self):
        self.init()
        result = self.execute('print("not-json")')
        self.assertEqual('failed', result['execution_status'])
        result = self.execute('import sys; sys.exit(4)')
        self.assertEqual(4, result['exit_code'])
        with self.assertRaises(WorkflowError): self.execute()

    def test_timeout(self):
        self.plan['timeout_seconds'] = 0.1; self.init()
        result = self.execute('import time; time.sleep(2)')
        self.assertEqual('timed-out', result['execution_status'])

    def test_lock(self):
        self.init()
        with self.engine.lock():
            with self.assertRaises(WorkflowError): self.engine.run('base', 'development', ['missing'])

    def evidence(self, reviewer='review-1'):
        return {'fingerprint': self.engine.fingerprint(self.engine.status()['tasks'][0]),
                'review': {'session_id': reviewer, 'independent': True, 'status': 'passed', 'artifact': 'review.txt'},
                'checks': [{'name': 'unit', 'status': 'passed', 'executed': 1, 'artifact': 'test.txt'}]}

    def test_independent_review_and_evidence_invalidation(self):
        self.init(); self.execute()
        with self.assertRaises(WorkflowError): self.engine.record_verification('base', self.evidence('dev-1'))
        self.engine.record_verification('base', self.evidence())
        self.engine.finish('base')
        self.assertEqual('later', self.engine.next_task()['id'])
        (self.root / 'base.txt').write_text('changed')
        self.assertEqual('base', self.engine.next_task()['id'])
        with self.assertRaises(WorkflowError): self.engine.finish('base')

    def test_budget_and_review_fresh_session(self):
        self.plan['max_calls'] = 2; self.init(); self.execute()
        result = self.execute('import sys,json; print(json.dumps({"session_id":"review-1","args":sys.argv}))', 'verification')
        self.assertIn('--mode', result['output']['args'])
        self.assertNotIn('--resume', result['output']['args'])
        with self.assertRaises(WorkflowError): self.execute(stage='verification')

    def test_out_of_scope_changes_blocked(self):
        self.init()
        result = self.execute('from pathlib import Path; Path("other.txt").write_text("oops"); print(\'{"result":"done"}\')')
        self.assertEqual('blocked', result['execution_status'])
        self.assertEqual(['other.txt'], result['out_of_scope'])

if __name__ == '__main__':
    unittest.main()
