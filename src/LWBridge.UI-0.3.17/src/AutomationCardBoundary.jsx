import { Component } from "react";

// R30: one failing Automation card must never blank the whole application.
export class AutomationCardBoundary extends Component {
  constructor(props) { super(props);this.state = { failed: false }; }
  static getDerivedStateFromError() { return { failed: true }; }
  componentDidCatch(error) { console.error("Automation card failed", error); }
  render() {
    if (!this.state.failed) return this.props.children;
    return <article className="automation-card" data-automation-card-failed={this.props.name}>
      <h3>{this.props.title}</h3>
      <p role="alert" className="error-text">{this.props.message}</p>
      <button type="button" onClick={() => this.setState({ failed: false })}>{this.props.retryLabel}</button>
    </article>;
  }
}
