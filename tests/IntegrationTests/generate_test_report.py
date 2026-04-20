#!/usr/bin/env python3
"""
Generate a readable HTML test report from integration test results.
Usage: python3 generate_test_report.py [output.html]
"""

import subprocess
import json
import re
import sys
from datetime import datetime
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[2]

def run_tests():
    """Run integration tests and capture results."""
    print("Running integration tests...")
    result = subprocess.run(
        ["dotnet", "test", "tests/IntegrationTests/IntegrationTests.csproj", 
         "--logger", "trx;LogFileName=test_results.trx",
         "--logger", "console;verbosity=detailed"],
        capture_output=True,
        text=True,
        cwd=REPO_ROOT
    )
    return result.stdout + result.stderr

def parse_test_output(output):
    """Parse test output to extract results."""
    tests = []
    lines = output.split('\n')
    
    current_test = None
    for i, line in enumerate(lines):
        # Match test result lines
        if '[xUnit.net' in line and ('PASS]' in line or 'FAIL]' in line):
            match = re.search(r'TokenizerIntegrationTests\.(\w+)\(configDir: "([^"]+)"\)', line)
            if match:
                test_method = match.group(1)
                config_path = match.group(2)
                config_name = Path(config_path).name
                
                passed = 'PASS]' in line
                
                # Extract duration
                duration_match = re.search(r'\[(\d+) ms\]', line)
                duration = duration_match.group(1) if duration_match else "0"
                
                # Extract error message if failed
                error_msg = None
                if not passed:
                    # Look ahead for error details
                    for j in range(i+1, min(i+20, len(lines))):
                        if 'Failure:' in lines[j] or 'Exception:' in lines[j]:
                            error_lines = []
                            for k in range(j, min(j+10, len(lines))):
                                if lines[k].strip():
                                    error_lines.append(lines[k].strip())
                                if 'Stack Trace:' in lines[k]:
                                    break
                            error_msg = '\n'.join(error_lines)
                            break
                
                tests.append({
                    'config': config_name,
                    'method': test_method,
                    'passed': passed,
                    'duration': duration,
                    'error': error_msg
                })
    
    return tests

def generate_html_report(tests, output_file):
    """Generate HTML report from test results."""
    
    # Group by configuration
    by_config = {}
    for test in tests:
        config = test['config']
        if config not in by_config:
            by_config[config] = []
        by_config[config].append(test)
    
    # Calculate statistics
    total = len(tests)
    passed = sum(1 for t in tests if t['passed'])
    failed = total - passed
    pass_rate = (passed / total * 100) if total > 0 else 0
    
    html = f"""<!DOCTYPE html>
<html>
<head>
    <title>KitsuMate.Tokenizers Integration Test Report</title>
    <style>
        body {{ font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; margin: 20px; background: #f5f5f5; }}
        .container {{ max-width: 1200px; margin: 0 auto; background: white; padding: 30px; border-radius: 8px; box-shadow: 0 2px 4px rgba(0,0,0,0.1); }}
        h1 {{ color: #333; border-bottom: 3px solid #4CAF50; padding-bottom: 10px; }}
        h2 {{ color: #555; margin-top: 30px; }}
        .summary {{ display: grid; grid-template-columns: repeat(auto-fit, minmax(200px, 1fr)); gap: 20px; margin: 20px 0; }}
        .stat-card {{ background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); color: white; padding: 20px; border-radius: 8px; text-align: center; }}
        .stat-card.passed {{ background: linear-gradient(135deg, #11998e 0%, #38ef7d 100%); }}
        .stat-card.failed {{ background: linear-gradient(135deg, #eb3349 0%, #f45c43 100%); }}
        .stat-card h3 {{ margin: 0; font-size: 2.5em; }}
        .stat-card p {{ margin: 5px 0 0 0; opacity: 0.9; }}
        .config-section {{ margin: 20px 0; border: 1px solid #ddd; border-radius: 8px; overflow: hidden; }}
        .config-header {{ background: #f8f9fa; padding: 15px; font-weight: bold; cursor: pointer; display: flex; justify-content: space-between; align-items: center; }}
        .config-header:hover {{ background: #e9ecef; }}
        .config-header .status {{ font-size: 0.9em; }}
        .pass-badge {{ background: #28a745; color: white; padding: 4px 12px; border-radius: 12px; font-size: 0.85em; }}
        .fail-badge {{ background: #dc3545; color: white; padding: 4px 12px; border-radius: 12px; font-size: 0.85em; }}
        .test-list {{ border-top: 1px solid #ddd; }}
        .test-item {{ padding: 12px 15px; border-bottom: 1px solid #eee; display: flex; justify-content: space-between; align-items: center; }}
        .test-item:last-child {{ border-bottom: none; }}
        .test-item.passed {{ background: #f0fff4; }}
        .test-item.failed {{ background: #fff5f5; }}
        .test-name {{ flex: 1; }}
        .test-duration {{ color: #666; font-size: 0.9em; margin: 0 15px; }}
        .test-status {{ font-weight: bold; }}
        .test-status.passed {{ color: #28a745; }}
        .test-status.failed {{ color: #dc3545; }}
        .error-details {{ margin-top: 10px; padding: 10px; background: #fff; border-left: 3px solid #dc3545; font-family: monospace; font-size: 0.85em; white-space: pre-wrap; }}
        .timestamp {{ color: #666; font-size: 0.9em; margin-top: 20px; text-align: center; }}
        .filter-buttons {{ margin: 20px 0; }}
        .filter-btn {{ padding: 8px 16px; margin: 0 5px; border: none; border-radius: 4px; cursor: pointer; background: #6c757d; color: white; }}
        .filter-btn.active {{ background: #007bff; }}
    </style>
</head>
<body>
    <div class="container">
        <h1>🧪 KitsuMate.Tokenizers Integration Test Report</h1>
        
        <div class="summary">
            <div class="stat-card">
                <h3>{total}</h3>
                <p>Total Tests</p>
            </div>
            <div class="stat-card passed">
                <h3>{passed}</h3>
                <p>Passed</p>
            </div>
            <div class="stat-card failed">
                <h3>{failed}</h3>
                <p>Failed</p>
            </div>
            <div class="stat-card">
                <h3>{pass_rate:.1f}%</h3>
                <p>Pass Rate</p>
            </div>
        </div>
        
        <h2>Test Results by Configuration</h2>
"""
    
    # Sort configs by pass rate (best first)
    sorted_configs = sorted(by_config.items(), 
                          key=lambda x: sum(1 for t in x[1] if t['passed']) / len(x[1]), 
                          reverse=True)
    
    for config, config_tests in sorted_configs:
        config_passed = sum(1 for t in config_tests if t['passed'])
        config_total = len(config_tests)
        config_rate = (config_passed / config_total * 100) if config_total > 0 else 0
        
        html += f"""
        <div class="config-section">
            <div class="config-header">
                <span>{config}</span>
                <span class="status">
                    <span class="{'pass-badge' if config_passed == config_total else 'fail-badge'}">
                        {config_passed}/{config_total} passed ({config_rate:.0f}%)
                    </span>
                </span>
            </div>
            <div class="test-list">
"""
        
        for test in config_tests:
            status_class = 'passed' if test['passed'] else 'failed'
            status_text = '✓ PASS' if test['passed'] else '✗ FAIL'
            
            html += f"""
                <div class="test-item {status_class}">
                    <span class="test-name">{test['method']}</span>
                    <span class="test-duration">{test['duration']}ms</span>
                    <span class="test-status {status_class}">{status_text}</span>
                </div>
"""
            if test['error']:
                html += f"""
                <div class="error-details">{test['error']}</div>
"""
        
        html += """
            </div>
        </div>
"""
    
    html += f"""
        <div class="timestamp">
            Generated: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}
        </div>
    </div>
</body>
</html>
"""
    
    with open(output_file, 'w') as f:
        f.write(html)
    
    print(f"\n✓ Report generated: {output_file}")
    print(f"  Total: {total}, Passed: {passed}, Failed: {failed}, Pass Rate: {pass_rate:.1f}%")

if __name__ == '__main__':
    output_file = sys.argv[1] if len(sys.argv) > 1 else 'test_report.html'
    
    output = run_tests()
    tests = parse_test_output(output)
    
    if not tests:
        print("Warning: No tests found in output")
        print("Output sample:", output[:500])
    else:
        generate_html_report(tests, output_file)
